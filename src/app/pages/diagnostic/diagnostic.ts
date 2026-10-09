import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { appSettings } from '../../content/app-settings';
import { site, whatsappLink } from '../../content/site-content';
import { FieldErrors, IdType, Subject, subjectNames, toApiError } from '../../core/api';
import { idNumberError, normaliseId, phoneValidator } from '../../core/validation';
import { WhatsappIcon } from '../../shared/icon';
import { Turnstile } from '../../shared/turnstile';
import { RegistrationApi, RegistrationOptions, RegistrationResponse } from './registration.service';

const SUBJECTS: Subject[] = ['Maths', 'PhysicalSciences'];

/** Fallback lists if the options call fails; the API validates against its own copy. */
const FALLBACK: RegistrationOptions = {
  sources: ['Google search', 'Facebook', 'Instagram', 'TikTok', 'WhatsApp', 'Friend or family', 'School', 'Other'],
  goals: ['Pass the subject', 'Improve my mark', 'Get a distinction', 'Qualify for engineering or science', 'Not sure yet'],
  subjects: [
    { subject: 'Maths', name: 'Mathematics', available: true, minutes: 45 },
    { subject: 'PhysicalSciences', name: 'Physical Sciences', available: true, minutes: 40 },
  ],
  maxLearners: 3,
  consentTextVersion: '0.3-draft',
};

const markValidators = [Validators.min(0), Validators.max(100), Validators.pattern(/^\d{1,3}$/)];

/** One learner. REG-02: at least one subject; the ID number is checked against its type (REG-09). */
function learnerGroup(fb: NonNullableFormBuilder) {
  return fb.group(
    {
      firstName: ['', [Validators.required, Validators.maxLength(60)]],
      surname: ['', [Validators.required, Validators.maxLength(60)]],
      school: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
      grade: fb.control<number | null>(null, Validators.required),
      maths: [false],
      mathsMark: fb.control<string>('', markValidators),
      physics: [false],
      physicsMark: fb.control<string>('', markValidators),
      goal: ['', Validators.required],
      idType: fb.control<IdType>('SA_ID'),
      idNumber: [''],
    },
    { validators: [learnerValidator] },
  );
}
type LearnerGroup = ReturnType<typeof learnerGroup>;

function learnerValidator(g: AbstractControl): ValidationErrors | null {
  const v = g.value as { maths: boolean; physics: boolean; idType: IdType; idNumber: string };
  const errors: ValidationErrors = {};
  if (!v.maths && !v.physics) errors['subjects'] = 'Choose at least one subject.';
  if (v.idNumber?.trim()) {
    const msg = idNumberError(v.idType, v.idNumber);
    if (msg) errors['idNumber'] = msg;
  }
  return Object.keys(errors).length ? errors : null;
}

/** REG-09: the same ID cannot appear twice in one registration. */
function duplicateIdValidator(arr: AbstractControl): ValidationErrors | null {
  const seen = new Map<string, number>();
  const dupes: number[] = [];
  (arr as FormArray<LearnerGroup>).controls.forEach((g, i) => {
    const id = normaliseId(g.controls.idNumber.value ?? '');
    if (!id) return;
    if (seen.has(id)) dupes.push(i);
    else seen.set(id, i);
  });
  return dupes.length ? { duplicateIds: dupes } : null;
}

@Component({
  selector: 'app-diagnostic',
  imports: [ReactiveFormsModule, RouterLink, WhatsappIcon, Turnstile],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './diagnostic.html',
  styleUrl: './diagnostic.scss',
})
export class Diagnostic {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly api = inject(RegistrationApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly turnstileWidget = viewChild(Turnstile);
  private readonly formTop = viewChild<ElementRef<HTMLElement>>('formTop');

  protected readonly site = site;
  protected readonly wa = whatsappLink();
  protected readonly subjectNames = subjectNames;
  protected readonly turnstileSiteKey = appSettings.turnstileSiteKey;

  protected readonly options = signal<RegistrationOptions>(FALLBACK);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly serverErrors = signal<FieldErrors>({});
  protected readonly formError = signal<string | null>(null);
  protected readonly result = signal<RegistrationResponse | null>(null);
  protected readonly turnstileToken = signal<string | null>(null);

  protected readonly form = this.fb.group({
    parent: this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
      phone: ['', [Validators.required, phoneValidator]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
      source: ['', Validators.required],
    }),
    learners: this.fb.array([learnerGroup(this.fb)], { validators: [duplicateIdValidator] }),
    isGuardianOver18: [false, Validators.requiredTrue],
    consentAccepted: [false, Validators.requiredTrue],
    website: [''], // honeypot (REG-06)
  });

  protected get learners() {
    return this.form.controls.learners;
  }

  protected readonly subjectsAvailable = computed(() =>
    Object.fromEntries(this.options().subjects.map((s) => [s.subject, s])) as Record<
      Subject,
      RegistrationOptions['subjects'][number] | undefined
    >,
  );

  constructor() {
    inject(Title).setTitle('Free Matric Readiness Diagnostic | Engineered Tutors');

    // Options only load in the browser: this page is prerendered at build time.
    afterNextRender(() => {
      this.api.options().subscribe({
        next: (o) => {
          this.options.set(o);
          for (const g of this.learners.controls) this.applySubjectAvailability(g);
        },
        error: () => {},
      });
    });

    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (Object.keys(this.serverErrors()).length) this.serverErrors.set({});
      this.formError.set(null);
    });
  }

  private applySubjectAvailability(g: LearnerGroup) {
    const a = this.subjectsAvailable();
    if (a.Maths && !a.Maths.available) g.controls.maths.disable();
    if (a.PhysicalSciences && !a.PhysicalSciences.available) g.controls.physics.disable();
  }

  protected addLearner() {
    if (this.learners.length >= this.options().maxLearners) return;
    const g = learnerGroup(this.fb);
    this.applySubjectAvailability(g);
    this.learners.push(g);
  }

  protected removeLearner(i: number) {
    if (this.learners.length > 1) this.learners.removeAt(i);
  }

  /** Error for a field: the server's message if it sent one, else the client check once the field is touched. */
  protected error(path: string): string | null {
    const server = this.serverErrors()[path];
    if (server?.length) return server[0];

    const c = this.controlFor(path);
    if (!c || !(c.touched || this.submitted()) || c.valid) return null;
    const e = c.errors ?? {};
    if (e['phone']) return e['phone'];
    if (e['required'])
      return (
        {
          'parent.fullName': 'Enter your full name.',
          'parent.phone': 'Enter your mobile number.',
          'parent.email': 'Enter your email address.',
          'parent.source': 'Tell us how you heard about us.',
          isGuardianOver18: 'Please confirm you are the parent or legal guardian and over 18.',
          consentAccepted: 'Please accept the consent statement to continue.',
        }[path] ??
        (path.endsWith('grade')
          ? 'Choose a grade.'
          : path.endsWith('goal')
            ? 'Choose a goal.'
            : path.endsWith('school')
              ? "Enter the learner's school."
              : 'This field is required.')
      );
    if (e['email']) return 'Enter a valid email address.';
    if (e['minlength']) return 'This is too short.';
    if (e['maxlength']) return 'This is too long.';
    if (e['min'] || e['max'] || e['pattern']) return 'Enter a mark from 0 to 100, or leave it blank.';
    return null;
  }

  /** Learner-level errors (subjects, ID number) that live on the group rather than a control. */
  protected learnerError(i: number, key: 'subjects' | 'idNumber'): string | null {
    const errs = this.serverErrors();
    const server = errs[`learners[${i}].${key}`] ?? (key === 'idNumber' ? errs[`learners[${i}].idType`] : undefined);
    if (server?.length) return server[0];
    const g = this.learners.at(i);
    const touched = this.submitted() || (key === 'idNumber' ? g.controls.idNumber.touched : g.touched);
    if (!touched) return null;
    if (key === 'idNumber') {
      const dupes = (this.learners.errors?.['duplicateIds'] as number[] | undefined) ?? [];
      if (dupes.includes(i)) return 'This ID number is already entered for another learner on this form.';
    }
    return (g.errors?.[key] as string | undefined) ?? null;
  }

  private controlFor(path: string): AbstractControl | null {
    const m = /^learners\[(\d+)\]\.(\w+)$/.exec(path);
    if (m) return (this.learners.at(+m[1]) as FormGroup).get(m[2]);
    return this.form.get(path);
  }

  protected submit() {
    this.submitted.set(true);
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.formError.set('Please fix the highlighted fields.');
      queueMicrotask(() => (document.querySelector('.field-error') as HTMLElement | null)?.scrollIntoView({ block: 'center', behavior: 'smooth' }));
      return;
    }
    if (this.turnstileSiteKey && !this.turnstileToken()) {
      this.formError.set('Please complete the security check.');
      return;
    }

    const v = this.form.getRawValue();
    const mark = (s: string) => (s.trim() === '' ? null : Number(s));
    this.submitting.set(true);
    this.api
      .register({
        parent: v.parent,
        learners: v.learners.map((l) => ({
          firstName: l.firstName,
          surname: l.surname,
          school: l.school,
          grade: l.grade,
          subjects: [
            ...(l.maths ? [{ subject: 'Maths' as Subject, latestMark: mark(l.mathsMark) }] : []),
            ...(l.physics ? [{ subject: 'PhysicalSciences' as Subject, latestMark: mark(l.physicsMark) }] : []),
          ],
          goal: l.goal,
          idType: l.idNumber.trim() ? l.idType : null,
          idNumber: l.idNumber.trim() || null,
        })),
        isGuardianOver18: v.isGuardianOver18,
        consentAccepted: v.consentAccepted,
        consentTextVersion: this.options().consentTextVersion,
        turnstileToken: this.turnstileToken(),
        website: v.website,
      })
      .subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.result.set(res);
          window.scrollTo({ top: 0, behavior: 'smooth' });
        },
        error: (err) => {
          this.submitting.set(false);
          const e = toApiError(err);
          this.turnstileWidget()?.reset();
          if (e.fieldErrors) {
            this.serverErrors.set(e.fieldErrors);
            this.formError.set(
              e.fieldErrors['turnstileToken']?.[0] ?? 'Please fix the highlighted fields.',
            );
          } else {
            this.formError.set(e.message);
          }
          this.formTop()?.nativeElement.scrollIntoView({ behavior: 'smooth' });
        },
      });
  }

  protected testPath(url: string) {
    return url.slice(url.indexOf('/d/'));
  }

  protected readonly subjects = SUBJECTS;
  protected readonly grades = [10, 11, 12];
  protected control(g: LearnerGroup, name: 'mathsMark' | 'physicsMark'): FormControl<string> {
    return g.controls[name];
  }
}
