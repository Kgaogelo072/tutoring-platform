import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';
import { ApiError, toApiError } from '../../core/api';
import { parseNumericAnswer } from '../../core/validation';
import { site, whatsappLink } from '../../content/site-content';
import { KatexLoader } from '../../shared/math-text';
import { AttemptApi, AttemptState, QuestionDto } from './attempt.service';
import { QuestionView } from './question-view';

type SaveState = 'saving' | 'saved' | 'error';

interface Answer {
  selectedOptionId: string | null;
  numericRaw: string;
}

/**
 * The learner's test page at /d/:token (DX-01 to DX-13). The server owns the clock and the answers; this page
 * shows a countdown, saves each answer as soon as it's given, and resumes from the server after a refresh.
 */
@Component({
  selector: 'app-test-page',
  imports: [QuestionView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './test-page.html',
  styleUrl: './test-page.scss',
})
export class TestPage implements OnInit {
  readonly token = input.required<string>();

  private readonly api = inject(AttemptApi);
  private readonly katex = inject(KatexLoader);
  private readonly title = inject(Title);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly wa = whatsappLink();
  protected readonly site = site;

  protected readonly phase = signal<'loading' | 'error' | 'instructions' | 'test' | 'done'>('loading');
  protected readonly error = signal<ApiError | null>(null);
  protected readonly state = signal<AttemptState | null>(null);
  protected readonly questions = signal<QuestionDto[]>([]);
  protected readonly index = signal(0);
  protected readonly answers = signal<Record<string, Answer>>({});
  protected readonly saves = signal<Record<string, SaveState>>({});
  protected readonly flagged = signal<Set<string>>(new Set());
  protected readonly now = signal(Date.now());
  protected readonly confirming = signal(false);
  protected readonly submitting = signal(false);
  protected readonly timeUp = signal(false);
  protected readonly starting = signal(false);
  protected readonly navOpen = signal(false);

  /** Server time minus local time, so the countdown follows the server clock (DX-07). */
  private clockOffset = 0;
  private deadline = 0;
  private timer?: ReturnType<typeof setInterval>;
  private readonly numericTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private readonly lastBody = new Map<string, { selectedOptionId?: string | null; numericRaw?: string | null }>();
  private autoSubmitting = false;

  protected readonly current = computed(() => this.questions()[this.index()] ?? null);
  protected readonly remainingMs = computed(() => Math.max(0, this.deadline - (this.now() + this.clockOffset)));
  protected readonly countdown = computed(() => {
    const s = Math.ceil(this.remainingMs() / 1000);
    const m = Math.floor(s / 60);
    return `${m}:${String(s % 60).padStart(2, '0')}`;
  });
  protected readonly lowTime = computed(() => this.remainingMs() <= 5 * 60_000);

  protected readonly answeredIds = computed(() => {
    const a = this.answers();
    return new Set(
      Object.entries(a)
        .filter(([, v]) => v.selectedOptionId || (v.numericRaw.trim() && parseNumericAnswer(v.numericRaw) !== null))
        .map(([k]) => k),
    );
  });
  protected readonly unanswered = computed(() =>
    this.questions().filter((q) => !this.answeredIds().has(q.id)).map((q) => q.number),
  );
  protected readonly flaggedNumbers = computed(() =>
    this.questions().filter((q) => this.flagged().has(q.id)).map((q) => q.number),
  );
  protected readonly unsaved = computed(() => Object.values(this.saves()).some((s) => s === 'error'));
  protected readonly currentSave = computed(() => {
    const q = this.current();
    return q ? this.saves()[q.id] : undefined;
  });

  ngOnInit() {
    this.title.setTitle('Diagnostic test | Engineered Tutors');
    void this.katex.load();
    this.restoreFlags();
    void this.load();

    const online = () => this.retryFailedSaves();
    window.addEventListener('online', online);
    this.destroyRef.onDestroy(() => {
      window.removeEventListener('online', online);
      if (this.timer) clearInterval(this.timer);
      this.numericTimers.forEach((t) => clearTimeout(t));
    });
  }

  private async load() {
    try {
      const s = await firstValueFrom(this.api.state(this.token()));
      this.applyState(s);
      if (s.status === 'InProgress') await this.loadQuestions();
    } catch (e) {
      this.fail(toApiError(e));
    }
  }

  protected retry() {
    this.error.set(null);
    this.phase.set('loading');
    void this.load();
  }

  private applyState(s: AttemptState) {
    this.state.set(s);
    this.clockOffset = Date.parse(s.serverNow) - Date.now();
    if (s.deadlineAt) this.deadline = Date.parse(s.deadlineAt);
    this.title.setTitle(`${s.subjectName} diagnostic | Engineered Tutors`);
    this.phase.set(s.status === 'NotStarted' ? 'instructions' : s.status === 'Submitted' ? 'done' : 'test');
  }

  private async loadQuestions() {
    const res = await firstValueFrom(this.api.questions(this.token()));
    this.deadline = Date.parse(res.deadlineAt);
    this.clockOffset = this.deadline - res.remainingSeconds * 1000 - Date.now();
    this.questions.set(res.questions);
    this.answers.set(
      Object.fromEntries(
        res.questions.map((q) => [q.id, { selectedOptionId: q.selectedOptionId, numericRaw: q.numericRaw ?? '' }]),
      ),
    );
    const firstOpen = res.questions.findIndex((q) => !q.selectedOptionId && !q.numericRaw);
    this.index.set(firstOpen >= 0 ? firstOpen : 0);
    this.phase.set('test');
    this.startTimer();
  }

  protected async start() {
    this.starting.set(true);
    try {
      this.applyState(await firstValueFrom(this.api.start(this.token())));
      await this.loadQuestions();
    } catch (e) {
      this.fail(toApiError(e));
    } finally {
      this.starting.set(false);
    }
  }

  private startTimer() {
    if (this.timer) clearInterval(this.timer);
    this.now.set(Date.now());
    this.timer = setInterval(() => {
      this.now.set(Date.now());
      if (this.remainingMs() <= 0 && !this.autoSubmitting) void this.finish(true);
    }, 1000);
  }

  // ---------- answers (DX-06, DX-11) ----------

  protected selectOption(q: QuestionDto, optionId: string) {
    this.setAnswer(q.id, { selectedOptionId: optionId });
    void this.save(q.id, { selectedOptionId: optionId });
  }

  protected numericInput(q: QuestionDto, raw: string) {
    this.setAnswer(q.id, { numericRaw: raw });
    const pending = this.numericTimers.get(q.id);
    if (pending) clearTimeout(pending);
    this.numericTimers.set(q.id, setTimeout(() => this.flushNumeric(q.id), 700));
  }

  protected flushNumeric(questionId: string) {
    const pending = this.numericTimers.get(questionId);
    if (pending) clearTimeout(pending);
    this.numericTimers.delete(questionId);
    const raw = this.answers()[questionId]?.numericRaw ?? '';
    if (raw.trim() && parseNumericAnswer(raw) === null) return; // flagged on screen, not saved
    void this.save(questionId, { numericRaw: raw.trim() ? raw : null });
  }

  private setAnswer(id: string, patch: Partial<Answer>) {
    this.answers.update((a) => ({ ...a, [id]: { ...(a[id] ?? { selectedOptionId: null, numericRaw: '' }), ...patch } }));
  }

  private async save(questionId: string, body: { selectedOptionId?: string | null; numericRaw?: string | null }) {
    this.lastBody.set(questionId, body);
    this.saves.update((s) => ({ ...s, [questionId]: 'saving' }));
    try {
      await firstValueFrom(this.api.save(this.token(), questionId, body));
      if (this.lastBody.get(questionId) === body) this.saves.update((s) => ({ ...s, [questionId]: 'saved' }));
    } catch (e) {
      const err = toApiError(e);
      if (err.code === 'time_up' || err.code === 'submitted') {
        this.timeUp.set(err.code === 'time_up');
        this.toDone();
        return;
      }
      if (this.lastBody.get(questionId) === body) this.saves.update((s) => ({ ...s, [questionId]: 'error' }));
    }
  }

  protected retryFailedSaves() {
    for (const [id, state] of Object.entries(this.saves())) {
      const body = this.lastBody.get(id);
      if (state === 'error' && body) void this.save(id, body);
    }
  }

  // ---------- navigation and flags (DX-03) ----------

  protected go(i: number) {
    const q = this.current();
    if (q?.type === 'NUM' && this.numericTimers.has(q.id)) this.flushNumeric(q.id);
    this.index.set(Math.max(0, Math.min(this.questions().length - 1, i)));
    this.navOpen.set(false);
    window.scrollTo({ top: 0 });
  }

  protected toggleFlag(q: QuestionDto) {
    this.flagged.update((f) => {
      const next = new Set(f);
      if (!next.delete(q.id)) next.add(q.id);
      return next;
    });
    try {
      localStorage.setItem(this.flagKey(), JSON.stringify([...this.flagged()]));
    } catch {
      /* storage unavailable: flags just won't survive a refresh */
    }
  }

  private flagKey = () => `et-flags-${this.token().slice(0, 12)}`;

  private restoreFlags() {
    try {
      const raw = localStorage.getItem(this.flagKey());
      if (raw) this.flagged.set(new Set(JSON.parse(raw) as string[]));
    } catch {
      /* ignore */
    }
  }

  protected statusOf(q: QuestionDto): string {
    const parts = [this.answeredIds().has(q.id) ? 'answered' : 'not answered'];
    if (this.flagged().has(q.id)) parts.push('flagged');
    return `Question ${q.number}, ${parts.join(', ')}`;
  }

  // ---------- submit (DX-09, DX-10, DX-12) ----------

  protected review() {
    const q = this.current();
    if (q?.type === 'NUM' && this.numericTimers.has(q.id)) this.flushNumeric(q.id);
    this.confirming.set(true);
  }

  protected async finish(automatic: boolean) {
    if (this.submitting()) return;
    if (automatic) this.autoSubmitting = true;
    this.submitting.set(true);
    [...this.numericTimers.keys()].forEach((id) => this.flushNumeric(id));
    try {
      await firstValueFrom(this.api.submit(this.token()));
      this.timeUp.set(automatic);
      this.toDone();
    } catch (e) {
      const err = toApiError(e);
      if (err.code === 'submitted' || err.code === 'time_up') {
        this.timeUp.set(automatic || err.code === 'time_up');
        this.toDone();
      } else {
        this.submitting.set(false);
        this.confirming.set(false);
        this.error.set(err);
        // Offline at the deadline: try again shortly. The server also submits on its own after the deadline.
        if (automatic) setTimeout(() => (this.autoSubmitting = false), 5000);
      }
    }
  }

  private toDone() {
    if (this.timer) clearInterval(this.timer);
    this.confirming.set(false);
    this.submitting.set(false);
    this.phase.set('done');
    try {
      localStorage.removeItem(this.flagKey());
    } catch {
      /* ignore */
    }
    window.scrollTo({ top: 0 });
  }

  private fail(err: ApiError) {
    this.error.set(err);
    this.phase.set('error');
  }
}
