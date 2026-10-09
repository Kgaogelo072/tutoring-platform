import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MathText } from '../../shared/math-text';
import { parseNumericAnswer } from '../../core/validation';
import { QuestionDto } from './attempt.service';

/**
 * One question as the learner sees it (DX-03, DX-04, DX-11). Used by the test page and the admin preview (AD-07).
 */
@Component({
  selector: 'app-question-view',
  imports: [MathText],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let q = question();
    <div class="q-head">
      <span class="q-num">Question {{ q.number }} of {{ total() }}</span>
      <span class="q-marks">{{ q.marks }} {{ q.marks === 1 ? 'mark' : 'marks' }}</span>
    </div>

    <div class="stem" [id]="'stem-' + q.id"><app-math [text]="q.stemLatex" /></div>

    @if (q.imageUrl && !imageFailed()) {
      <img class="figure" [src]="q.imageUrl" [alt]="'Diagram for question ' + q.number" loading="lazy"
           (error)="imageFailed.set(true)" />
    }

    @if (q.type === 'MCQ') {
      <div class="options" role="radiogroup" [attr.aria-labelledby]="'stem-' + q.id">
        @for (o of q.options; track o.id; let i = $index) {
          <label class="option" [class.selected]="selected() === o.id">
            <input type="radio" [name]="'q-' + q.id" [value]="o.id" [checked]="selected() === o.id"
                   [disabled]="disabled()" (change)="optionSelected.emit(o.id)" />
            <span class="letter" aria-hidden="true">{{ letters[i] }}</span>
            <span class="text"><app-math [text]="o.textLatex" /></span>
          </label>
        }
      </div>
    } @else {
      <div class="numeric">
        <label class="sr-only" [for]="'num-' + q.id">Your answer</label>
        <div class="num-row" [class.invalid]="invalid()">
          <button type="button" class="sign" (click)="toggleSign()" [disabled]="disabled()"
                  aria-label="Make the number negative or positive">±</button>
          <input [id]="'num-' + q.id" type="text" inputmode="decimal" autocomplete="off" autocorrect="off"
                 spellcheck="false" maxlength="24" [value]="raw()" [disabled]="disabled()"
                 [attr.aria-invalid]="invalid()" [attr.aria-describedby]="'num-help-' + q.id"
                 (input)="onInput($any($event.target).value)" (blur)="numericBlur.emit()" />
          @if (q.unit) { <span class="unit"><app-math [text]="q.unit" /></span> }
        </div>
        @if (invalid()) {
          <p class="num-error" role="alert">
            Type a number only, e.g. -4 or 5,05. Leave out units and other symbols. This answer isn't saved yet.
          </p>
        } @else {
          <p class="num-help" [id]="'num-help-' + q.id">Type only the number. Use a comma or a full stop for decimals.</p>
        }
      </div>
    }
  `,
  styleUrl: './question-view.scss',
})
export class QuestionView {
  readonly question = input.required<QuestionDto>();
  readonly total = input.required<number>();
  readonly selected = input<string | null>(null);
  readonly raw = input<string>('');
  readonly disabled = input(false);

  readonly optionSelected = output<string>();
  readonly numericInput = output<string>();
  readonly numericBlur = output<void>();

  protected readonly letters = ['A', 'B', 'C', 'D', 'E', 'F'];
  protected readonly imageFailed = signal(false);
  protected readonly invalid = computed(() => {
    const r = this.raw();
    return !!r.trim() && parseNumericAnswer(r) === null;
  });

  protected onInput(value: string) {
    this.numericInput.emit(value);
  }

  protected toggleSign() {
    const r = this.raw().trim();
    this.numericInput.emit(r.startsWith('-') || r.startsWith('−') ? r.slice(1) : '-' + r);
  }
}
