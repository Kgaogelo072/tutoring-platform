import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toApiError } from '../core/api';
import { Icon } from '../shared/icon';
import { KatexLoader } from '../shared/math-text';
import { QuestionView } from '../pages/test/question-view';
import { AdminApi, Preview } from './admin-api';

/**
 * AD-07: shows an assessment exactly as a learner sees it, one question at a time, without creating an attempt.
 * Answers chosen here stay in the browser and are never sent.
 */
@Component({
  selector: 'app-preview-page',
  imports: [RouterLink, QuestionView, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (preview(); as p) {
      <section class="hero" [class.science]="p.subject === 'PhysicalSciences'">
        <div class="hero-inner">
          <a routerLink="/admin/questions" class="back">← Back to Questions</a>
          <h1>{{ p.subjectName }} v{{ p.version }} <span class="badge">Preview</span></h1>
          <p class="meta">{{ p.questions.length }} questions · {{ p.timeLimitMinutes }} minutes · {{ p.publishedAt ? 'Published' : 'Draft' }}</p>
          <p class="note">Nothing you choose here is saved.</p>
        </div>
      </section>

      <div class="layout">
        @for (q of [p.questions[index()]]; track q.id) {
          <article class="card question">
            <app-question-view [question]="q" [total]="p.questions.length"
                               [selected]="selected()[q.id] ?? null" [raw]="raw()[q.id] ?? ''"
                               (optionSelected)="select(q.id, $event)" (numericInput)="type(q.id, $event)">
              <button qActions type="button" class="mark" [class.on]="flagged().has(q.id)" (click)="toggleFlag(q.id)"
                      [attr.aria-pressed]="flagged().has(q.id)" aria-label="Flag this question">
                <app-icon name="bookmark" [size]="20" />
              </button>
            </app-question-view>

            <div class="pager">
              <button type="button" class="btn btn-outline" (click)="index.set(index() - 1)" [disabled]="index() === 0">← Previous</button>
              <button type="button" class="btn btn-gold" (click)="index.set(index() + 1)" [disabled]="index() === p.questions.length - 1">Next →</button>
            </div>
          </article>
        }

        <aside class="card progress" aria-label="Quiz progress">
          <div class="progress-head">
            <h2>Quiz Progress</h2>
            <span class="muted small">{{ index() + 1 }} of {{ p.questions.length }}</span>
          </div>
          <div class="bar" role="progressbar" [attr.aria-valuenow]="percent()" aria-valuemin="0" aria-valuemax="100">
            <span [style.width.%]="percent()"></span>
          </div>
          <p class="muted tiny">{{ answered() }} answered · {{ percent() }}% completed</p>

          <div class="grid">
            @for (q of p.questions; track q.id; let i = $index) {
              <button type="button" class="dot" [class.current]="i === index()" [class.done]="isAnswered(q.id)"
                      [class.flagged]="flagged().has(q.id)" (click)="index.set(i)"
                      [attr.aria-label]="'Question ' + (i + 1)" [attr.aria-current]="i === index() ? 'step' : null">{{ i + 1 }}</button>
            }
          </div>

          <dl class="facts">
            <div><app-icon name="clock" [size]="26" /><dt>Time limit</dt><dd>{{ p.timeLimitMinutes }} min</dd></div>
            <div><app-icon name="award" [size]="26" /><dt>Total marks</dt><dd>{{ totalMarks() }}</dd></div>
          </dl>
        </aside>
      </div>
    } @else if (error()) {
      <p class="back-plain"><a routerLink="/admin/questions">← Questions</a></p>
      <p class="alert">{{ error() }}</p>
    } @else {
      <p class="muted loading">Loading…</p>
    }
  `,
  styleUrl: './admin-shared.scss',
  styles: `
    /* Full-bleed banner inside the admin shell's centred column. */
    .hero {
      margin: 0 calc(50% - 50vw) 28px;
      color: #fff;
      background:
        linear-gradient(90deg, var(--navy) 0%, var(--navy) 38%, rgba(12, 31, 69, 0.55) 70%, rgba(12, 31, 69, 0.15) 100%),
        url('/img/maths-notebook-1280.webp') right center / cover no-repeat,
        var(--navy);
    }
    .hero.science {
      background:
        linear-gradient(90deg, var(--navy) 0%, var(--navy) 38%, rgba(12, 31, 69, 0.55) 70%, rgba(12, 31, 69, 0.15) 100%),
        url('/img/lab-desk-1280.webp') right center / cover no-repeat,
        var(--navy);
    }
    .hero-inner { max-width: var(--container); margin: 0 auto; padding: 36px var(--gutter) 40px; }
    .back { color: #fff; font-weight: 500; font-size: 0.92rem; text-decoration: none; opacity: 0.9; }
    .back:hover { opacity: 1; text-decoration: underline; }
    .hero h1 { color: #fff; margin: 12px 0 8px; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; }
    .badge {
      font-family: var(--font-body); font-size: 0.82rem; font-weight: 600; letter-spacing: 0.02em;
      padding: 4px 14px; border-radius: 999px; background: var(--gold); color: var(--ink);
    }
    .meta { margin: 0 0 4px; font-size: 1.05rem; color: #fff; }
    .note { margin: 0; color: rgba(255, 255, 255, 0.72); }

    .layout { display: grid; gap: 20px; align-items: start; }
    .card { margin: 0; border-radius: 16px; border-color: #e6e9ef; box-shadow: 0 6px 20px rgba(12, 31, 69, 0.05); }
    .question { padding: 24px; min-width: 0; }

    .mark {
      display: inline-grid; place-items: center; width: 36px; height: 36px; padding: 0;
      border: 0; border-radius: 8px; background: none; color: var(--navy); cursor: pointer;
    }
    .mark:hover { background: #f1f3f8; }
    .mark.on { color: var(--gold); }
    .mark.on ::ng-deep path { fill: currentColor; }

    .pager { display: flex; justify-content: space-between; gap: 12px; margin-top: 24px; }
    .pager .btn { min-width: 128px; }
    .pager .btn-outline { border: 1.5px solid #d5dae3; color: var(--muted); }
    .pager .btn-outline:not([disabled]):hover { border-color: var(--navy); color: var(--navy); }
    .pager .btn-gold { box-shadow: none; }

    .progress { padding: 20px; }
    .progress-head { display: flex; justify-content: space-between; align-items: baseline; gap: 12px; }
    .progress h2 { font-family: var(--font-body); font-size: 1.05rem; font-weight: 700; margin: 0; }
    .bar { height: 6px; border-radius: 999px; background: #eceff4; margin: 14px 0 8px; overflow: hidden; }
    .bar span { display: block; height: 100%; min-width: 6px; border-radius: inherit; background: var(--navy); transition: width 0.2s ease; }
    .tiny { font-size: 0.78rem; margin: 0 0 16px; }

    .grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 10px; justify-items: center; }
    .dot {
      width: 40px; height: 40px; border-radius: 50%; border: 1.5px solid #d5dae3; background: #fff;
      font: inherit; font-size: 0.88rem; font-weight: 600; color: var(--navy); cursor: pointer; position: relative;
    }
    .dot:hover { border-color: var(--navy); }
    .dot.done { background: var(--gold-soft); border-color: var(--gold-soft); }
    .dot.current { background: var(--navy); border-color: var(--navy); color: #fff; }
    .dot.flagged::after {
      content: ''; position: absolute; top: -2px; right: -2px; width: 10px; height: 10px;
      border-radius: 50%; background: var(--gold); border: 2px solid #fff;
    }

    .facts { margin: 20px 0 0; padding-top: 18px; border-top: 1px solid #eceff4; display: grid; gap: 16px; }
    .facts div { display: grid; grid-template-columns: auto 1fr; column-gap: 14px; align-items: center; }
    .facts app-icon { grid-row: span 2; color: var(--navy); }
    .facts dt { font-size: 0.85rem; color: var(--muted); }
    .facts dd { margin: 0; font-size: 1.25rem; font-weight: 700; color: var(--navy); }

    .back-plain { margin: 20px 0 8px; }
    .loading { padding: 32px 0; }

    @media (min-width: 960px) {
      .layout { grid-template-columns: minmax(0, 1fr) 280px; gap: 24px; }
      .progress { position: sticky; top: 96px; }
      .question { padding: 28px 32px; }
    }
    @media (max-width: 480px) {
      .question { padding: 18px; }
      .pager .btn { min-width: 0; flex: 1; }
      .dot { width: 44px; height: 44px; }
    }
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
  protected readonly flagged = signal<ReadonlySet<string>>(new Set());

  protected readonly answered = computed(
    () => this.preview()?.questions.filter((q) => this.isAnswered(q.id)).length ?? 0,
  );
  protected readonly percent = computed(() => {
    const total = this.preview()?.questions.length ?? 0;
    return total ? Math.round((this.answered() / total) * 100) : 0;
  });
  protected readonly totalMarks = computed(() => this.preview()?.questions.reduce((sum, q) => sum + q.marks, 0) ?? 0);

  constructor() {
    void inject(KatexLoader).load();
  }

  ngOnInit() {
    this.api.preview(this.id()).subscribe({
      next: (p) => this.preview.set(p),
      error: (e) => this.error.set(toApiError(e).message),
    });
  }

  protected isAnswered(id: string) {
    return !!this.selected()[id] || !!this.raw()[id]?.trim();
  }

  protected select(id: string, option: string) {
    this.selected.update((s) => ({ ...s, [id]: option }));
  }

  protected type(id: string, value: string) {
    this.raw.update((r) => ({ ...r, [id]: value }));
  }

  protected toggleFlag(id: string) {
    this.flagged.update((f) => {
      const next = new Set(f);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }
}
