import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, sast, subjectNames, toApiError } from '../core/api';
import { AdminApi, AssessmentSummary, ImportError, ImportResult } from './admin-api';

/** AD-06 import, SC-06 versions and publishing, DX-13 time limits, AD-07 preview links. */
@Component({
  selector: 'app-questions-page',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1 class="head">Questions</h1>
    <p class="muted">
      Each import creates a new draft version. Publishing makes it the version new registrations get, and freezes it:
      to fix a question, import a corrected file and publish that.
    </p>

    @if (error()) { <p class="alert">{{ error() }}</p> }

    <section class="card">
      <h2>Versions</h2>
      <div class="table-wrap">
        <table>
          <thead>
            <tr><th>Subject</th><th>Version</th><th>Questions</th><th>Time limit</th><th>Status</th><th>Attempts</th><th></th></tr>
          </thead>
          <tbody>
            @for (a of assessments(); track a.id) {
              <tr>
                <td>{{ subjectNames[a.subject] }}</td>
                <td>v{{ a.version }}</td>
                <td>{{ a.questionCount }} · {{ a.totalMarks }} marks</td>
                <td>
                  @if (a.publishedAt) {
                    {{ a.timeLimitMinutes }} min
                  } @else {
                    <input type="number" min="5" max="180" class="minutes" [ngModel]="a.timeLimitMinutes"
                           (change)="setMinutes(a, $any($event.target).value)" aria-label="Time limit in minutes" /> min
                  }
                </td>
                <td>
                  @if (a.publishedAt) {
                    <span class="chip Submitted">Published</span>
                    <div class="muted small">{{ sast(a.publishedAt) }}</div>
                    @if (isCurrent(a)) { <div class="muted small">current</div> }
                  } @else {
                    <span class="chip">Draft</span>
                  }
                </td>
                <td>{{ a.attemptCount }}</td>
                <td class="row">
                  <a class="btn btn-outline btn-sm" [routerLink]="['/admin/assessments', a.id, 'preview']">Preview</a>
                  @if (!a.publishedAt) {
                    <button type="button" class="btn btn-navy btn-sm" (click)="publish(a)" [disabled]="busy()">Publish</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="muted">No question sets yet. Import one below.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    <section class="card">
      <h2>Import a question file</h2>
      <p class="muted small">
        CSV in the section 5.2 format, UTF-8, every field quoted. Nothing is imported unless every row is valid.
      </p>
      <form class="row" (ngSubmit)="import()">
        <select [(ngModel)]="subject" name="subject" aria-label="Subject" (ngModelChange)="minutes = $event === 'Maths' ? 45 : 40">
          <option value="Maths">Mathematics</option>
          <option value="PhysicalSciences">Physical Sciences</option>
        </select>
        <label class="row small">Time limit
          <input type="number" min="5" max="180" [(ngModel)]="minutes" name="minutes" class="minutes" /> min
        </label>
        <input type="file" accept=".csv,text/csv" (change)="file = $any($event.target).files?.[0] ?? null" aria-label="CSV file" />
        <button type="submit" class="btn btn-gold" [disabled]="!file || busy()">{{ busy() ? 'Importing…' : 'Import' }}</button>
      </form>

      @if (imported(); as r) {
        <p class="ok">
          Imported {{ subjectNames[r.subject] }} v{{ r.version }}: {{ r.questionCount }} questions, {{ r.totalMarks }} marks.
          It's a draft: preview it, then publish.
        </p>
      }
      @if (importErrors().length) {
        <p class="alert">Nothing was imported. Fix these rows and try again.</p>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Row</th><th>Column</th><th>Problem</th></tr></thead>
            <tbody>
              @for (e of importErrors(); track $index) {
                <tr><td>{{ e.row || '—' }}</td><td>{{ e.column || '—' }}</td><td>{{ e.message }}</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
  `,
  styleUrl: './admin-shared.scss',
  styles: `
    .head { margin-top: 24px; }
    .minutes { width: 80px; }
    td.row { flex-wrap: nowrap; }
  `,
})
export class QuestionsPage implements OnInit {
  private readonly api = inject(AdminApi);
  protected readonly subjectNames = subjectNames;
  protected readonly sast = sast;

  protected readonly assessments = signal<AssessmentSummary[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly imported = signal<ImportResult | null>(null);
  protected readonly importErrors = signal<ImportError[]>([]);

  protected subject: Subject = 'Maths';
  protected minutes = 45;
  protected file: File | null = null;

  ngOnInit() {
    this.load();
  }

  private load() {
    this.api.assessments().subscribe({
      next: (a) => this.assessments.set(a),
      error: (e) => this.error.set(toApiError(e).message),
    });
  }

  /** The latest published version per subject is what new registrations get. */
  protected isCurrent(a: AssessmentSummary) {
    const published = this.assessments().filter((x) => x.subject === a.subject && x.publishedAt);
    return published.length > 0 && Math.max(...published.map((x) => x.version)) === a.version;
  }

  protected import() {
    if (!this.file) return;
    this.busy.set(true);
    this.imported.set(null);
    this.importErrors.set([]);
    this.error.set(null);
    this.api.importQuestions(this.file, this.subject, this.minutes).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.imported.set(r);
        this.load();
      },
      error: (e: unknown) => {
        this.busy.set(false);
        if (e instanceof HttpErrorResponse && e.status === 422) this.importErrors.set(e.error?.errors ?? []);
        else {
          const err = toApiError(e);
          this.error.set(err.fieldErrors ? Object.values(err.fieldErrors).flat().join(' ') : err.message);
        }
      },
    });
  }

  protected setMinutes(a: AssessmentSummary, value: string) {
    const m = Number(value);
    this.api.updateAssessment(a.id, m).subscribe({
      next: () => this.load(),
      error: (e) => {
        const err = toApiError(e);
        this.error.set(err.fieldErrors ? Object.values(err.fieldErrors).flat().join(' ') : err.message);
        this.load();
      },
    });
  }

  protected publish(a: AssessmentSummary) {
    const msg =
      `Publish ${subjectNames[a.subject]} v${a.version}? New registrations will get this version, ` +
      `and its questions can't be changed afterwards.`;
    if (!window.confirm(msg)) return;
    this.busy.set(true);
    this.api.publish(a.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(toApiError(e).message);
      },
    });
  }
}
