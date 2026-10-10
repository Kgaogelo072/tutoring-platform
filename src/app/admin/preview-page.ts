import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toApiError } from '../core/api';
import { KatexLoader } from '../shared/math-text';
import { QuestionView } from '../pages/test/question-view';
import { AdminApi, Preview } from './admin-api';

/**
 * AD-07: shows an assessment exactly as a learner sees it, one question at a time, without creating an attempt.
 * Answers chosen here stay in the browser and are never sent.
 */
@Component({
  selector: 'app-preview-page',
  imports: [RouterLink, QuestionView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="back"><a routerLink="/admin/questions">← Questions</a></p>
    @if (error()) { <p class="alert">{{ error() }}</p> }
    @if (preview(); as p) {
      <h1>{{ p.subjectName }} v{{ p.version }} <span class="muted small">preview</span></h1>
      <p class="muted">{{ p.questions.length }} questions · {{ p.timeLimitMinutes }} minutes · {{ p.publishedAt ? 'published' : 'draft' }}.
        Nothing you choose here is saved.</p>

      @for (q of [p.questions[index()]]; track q.id) {
        <article class="card learner">
          <app-question-view [question]="q" [total]="p.questions.length"
                             [selected]="selected()[q.id] ?? null" [raw]="raw()[q.id] ?? ''"
                             (optionSelected)="select(q.id, $event)" (numericInput)="type(q.id, $event)" />
        </article>
      }

      <div class="row pager">
        <button type="button" class="btn btn-outline" (click)="index.set(index() - 1)" [disabled]="index() === 0">← Previous</button>
        <span class="muted">{{ index() + 1 }} / {{ p.questions.length }}</span>
        <button type="button" class="btn btn-navy" (click)="index.set(index() + 1)" [disabled]="index() === p.questions.length - 1">Next →</button>
      </div>
      <div class="jump row">
        @for (q of p.questions; track q.id; let i = $index) {
          <button type="button" class="btn btn-sm" [class.btn-navy]="i === index()" [class.btn-outline]="i !== index()" (click)="index.set(i)">{{ i + 1 }}</button>
        }
      </div>
    } @else if (!error()) {
      <p class="muted">Loading…</p>
    }
  `,
  styleUrl: './admin-shared.scss',
  styles: `
    .back { margin: 20px 0 8px; }
    .learner { max-width: 760px; padding: 24px; }
    .pager { max-width: 760px; justify-content: space-between; }
    .jump { margin-top: 16px; gap: 6px; }
    .jump .btn { min-width: 44px; }
  `,
})
export class PreviewPage implements OnInit {
  readonly id = input.required<string>();
  private readonly api = inject(AdminApi);
  protected readonly preview = signal<Preview | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly index = signal(0);
  protected readonly selected = signal<Record<string, string | undefined>>({});
  protected readonly raw = signal<Record<string, string | undefined>>({});

  constructor() {
    void inject(KatexLoader).load();
  }

  ngOnInit() {
    this.api.preview(this.id()).subscribe({
      next: (p) => this.preview.set(p),
      error: (e) => this.error.set(toApiError(e).message),
    });
  }

  protected select(id: string, option: string) {
    this.selected.update((s) => ({ ...s, [id]: option }));
  }

  protected type(id: string, value: string) {
    this.raw.update((r) => ({ ...r, [id]: value }));
  }
}
