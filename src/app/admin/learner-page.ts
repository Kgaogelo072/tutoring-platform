import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LeadStatus, bandNames, leadStatusNames, sast, subjectNames, toApiError } from '../core/api';
import { KatexLoader, MathText } from '../shared/math-text';
import { AdminApi, LearnerDetail, whatsappToParent } from './admin-api';

/** AD-04 learner detail: topic scores and bands, every answer, masked ID number; AD-03 status and notes. */
@Component({
  selector: 'app-learner-page',
  imports: [FormsModule, RouterLink, MathText],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="back"><a routerLink="/admin">← Leads</a></p>
    @if (error()) { <p class="alert">{{ error() }}</p> }

    @if (detail(); as d) {
      <h1>{{ d.learner.firstName }} {{ d.learner.surname }}</h1>
      <p class="muted">
        Grade {{ d.learner.grade }} · {{ d.learner.school }} · Goal: {{ d.learner.goal }}
        @if (d.learner.idMasked) { · {{ d.learner.idType === 'PASSPORT' ? 'Passport' : 'ID' }} {{ d.learner.idMasked }} }
        · registered {{ sast(d.learner.createdAt) }}
      </p>

      <div class="grid">
        <section class="card">
          <h2>Parent</h2>
          <p><strong>{{ d.parent.fullName }}</strong><br />
            {{ d.parent.phone }}<br />{{ d.parent.email }}<br />
            <span class="muted small">Heard via {{ d.parent.source }}</span></p>
          <div class="row">
            <button type="button" class="btn btn-outline btn-sm" (click)="copy(d.parent.phone)">{{ copied() ? 'Copied ✓' : 'Copy phone' }}</button>
            <a class="btn btn-wa btn-sm" [href]="wa(d)" target="_blank" rel="noopener">WhatsApp</a>
          </div>
          <p class="muted small marks">
            Latest marks:
            @for (s of d.learner.subjects; track s.subject) {
              {{ subjectNames[s.subject] }} {{ s.latestMark ?? '—' }}{{ s.latestMark !== null ? '%' : '' }}{{ $last ? '' : ' · ' }}
            }
          </p>
          @if (d.consents[0]; as c) {
            <p class="muted small">Consent v{{ c.textVersion }}, {{ sast(c.acceptedAt) }}, IP {{ c.ipAddress }}</p>
          }
        </section>

        @if (d.lead; as lead) {
          <section class="card">
            <h2>Lead</h2>
            <label class="small muted" for="status">Status</label>
            <select id="status" [(ngModel)]="status" class="full">
              @for (s of statuses; track s) { <option [value]="s">{{ statusNames[s] }}</option> }
            </select>
            <label class="small muted" for="notes">Notes</label>
            <textarea id="notes" [(ngModel)]="notes" maxlength="4000"></textarea>
            <div class="row">
              <button type="button" class="btn btn-navy btn-sm" (click)="saveLead(lead.id)" [disabled]="saving()">
                {{ saving() ? 'Saving…' : 'Save' }}
              </button>
              @if (saved()) { <span class="muted small">Saved ✓</span> }
            </div>
          </section>
        }
      </div>

      @for (a of d.attempts; track a.id) {
        <section class="card">
          <div class="row">
            <h2>{{ subjectNames[a.subject] }} <span class="muted small">v{{ a.assessmentVersion }}</span></h2>
            <span class="spacer"></span>
            <span [class]="'chip ' + a.status">{{ a.status === 'NotStarted' ? 'Not started' : a.status === 'InProgress' ? 'In progress' : 'Submitted' }}</span>
          </div>

          @if (a.status === 'Submitted') {
            <p class="overall"><strong>{{ a.overallPct }}%</strong> overall</p>
            <p class="muted small">
              Started {{ sast(a.startedAt) }} · submitted {{ sast(a.submittedAt) }}
              @if (a.timeTakenSeconds !== null) { · {{ minutes(a.timeTakenSeconds) }} }
            </p>

            <div class="table-wrap">
              <table>
                <thead><tr><th>Topic</th><th>Marks</th><th>Score</th><th>Band</th></tr></thead>
                <tbody>
                  @for (t of a.topics; track t.code) {
                    <tr>
                      <td>{{ t.name }}</td>
                      <td>{{ t.marksEarned }}/{{ t.marksAvailable }}</td>
                      <td>{{ t.pct }}%</td>
                      <td><span [class]="'chip ' + t.band">{{ bandNames[t.band] }}</span></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <details>
              <summary>Every question ({{ a.questions.length }})</summary>
              <div class="table-wrap">
                <table class="answers">
                  <thead><tr><th>#</th><th>Question</th><th>Answer given</th><th>Correct answer</th><th></th></tr></thead>
                  <tbody>
                    @for (q of a.questions; track q.number) {
                      <tr [class.wrong]="!q.isCorrect">
                        <td>{{ q.number }}<div class="muted small">{{ q.code }}</div></td>
                        <td class="q"><app-math [text]="q.stemLatex" /><div class="muted small">{{ q.topic }}</div></td>
                        <td>
                          @if (q.answerGiven) { <app-math [text]="q.answerGiven" /> } @else { <span class="muted">—</span> }
                          @if (q.answeredAt) { <div class="muted small">{{ sast(q.answeredAt) }}</div> }
                        </td>
                        <td><app-math [text]="q.correctAnswer" /></td>
                        <td>{{ q.isCorrect ? '✓' : '✗' }} {{ q.marksAwarded }}/{{ q.marks }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </details>
          } @else {
            <p class="muted small">
              Link issued {{ sast(a.createdAt) }}, expires {{ sast(a.tokenExpiresAt) }}.
              @if (a.startedAt) { Started {{ sast(a.startedAt) }}, deadline {{ sast(a.deadlineAt) }}. }
            </p>
          }
        </section>
      }
    } @else if (!error()) {
      <p class="muted">Loading…</p>
    }
  `,
  styleUrl: './admin-shared.scss',
  styles: `
    .back { margin: 20px 0 8px; }
    .grid { display: grid; gap: 0 16px; }
    @media (min-width: 900px) { .grid { grid-template-columns: 1fr 1fr; } }
    .full { width: 100%; margin-bottom: 10px; }
    label { display: block; margin: 4px 0; }
    .marks { margin-top: 12px; }
    .overall { font-size: 1.6rem; margin: 4px 0; color: var(--navy); }
    details { margin-top: 16px; }
    summary { cursor: pointer; font-weight: 700; color: var(--navy); min-height: 44px; display: flex; align-items: center; }
    .answers .q { min-width: 260px; }
    tr.wrong td:last-child { color: #b42318; font-weight: 700; }
  `,
})
export class LearnerPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(AdminApi);

  protected readonly subjectNames = subjectNames;
  protected readonly bandNames = bandNames;
  protected readonly statusNames = leadStatusNames;
  protected readonly statuses = Object.keys(leadStatusNames) as LeadStatus[];
  protected readonly sast = sast;

  protected readonly detail = signal<LearnerDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly copied = signal(false);
  protected status: LeadStatus = 'New';
  protected notes = '';

  constructor() {
    void inject(KatexLoader).load();
  }

  ngOnInit() {
    this.api.learner(this.id()).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.status = d.lead?.status ?? 'New';
        this.notes = d.lead?.notes ?? '';
      },
      error: (e) => this.error.set(toApiError(e).status === 404 ? 'Learner not found.' : toApiError(e).message),
    });
  }

  protected saveLead(id: string) {
    this.saving.set(true);
    this.saved.set(false);
    this.api.updateLead(id, { status: this.status, notes: this.notes }).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
      },
      error: (e) => {
        this.saving.set(false);
        this.error.set(toApiError(e).message);
      },
    });
  }

  protected wa(d: LearnerDetail) {
    return whatsappToParent(d.parent.phone, d.parent.fullName, d.learner.firstName);
  }

  protected async copy(phone: string) {
    try {
      await navigator.clipboard.writeText(phone);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      window.prompt('Copy the number:', phone);
    }
  }

  protected minutes(s: number) {
    return `${Math.floor(s / 60)} min ${s % 60} s`;
  }
}
