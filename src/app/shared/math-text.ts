import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  Injectable,
  PLATFORM_ID,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

type Katex = typeof import('katex');

/**
 * Loads KaTeX (script and stylesheet) the first time a maths page needs it, so the landing pages never pay for it
 * (NF-02, NF-03). The stylesheet and fonts are copied to /katex by angular.json and cached by the browser.
 */
@Injectable({ providedIn: 'root' })
export class KatexLoader {
  private readonly doc = inject(DOCUMENT);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));
  private loading?: Promise<Katex | null>;
  readonly katex = signal<Katex | null>(null);

  load(): Promise<Katex | null> {
    if (!this.browser) return Promise.resolve(null);
    if (!this.loading) {
      if (!this.doc.querySelector('link[data-katex]')) {
        const link = this.doc.createElement('link');
        link.rel = 'stylesheet';
        link.href = 'katex/katex.min.css';
        link.dataset['katex'] = '';
        this.doc.head.appendChild(link);
      }
      this.loading = import('katex').then((m) => {
        const k = (m as unknown as { default?: Katex }).default ?? m;
        this.katex.set(k);
        return k;
      });
    }
    return this.loading;
  }
}

const escapeHtml = (s: string) =>
  s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

interface Segment {
  math: boolean;
  display: boolean;
  text: string;
}

/** Splits text on unescaped $ (inline) and $$ (display) delimiters. \$ is a literal dollar sign. */
export function splitMath(text: string): Segment[] {
  const out: Segment[] = [];
  let buf = '';
  let inMath = false;
  let display = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '\\' && text[i + 1] === '$') {
      buf += inMath ? '\\$' : '$';
      i++;
      continue;
    }
    if (c === '$') {
      const dbl = text[i + 1] === '$';
      if (buf || inMath) out.push({ math: inMath, display, text: buf });
      buf = '';
      if (inMath) {
        inMath = false;
        if (display && dbl) i++;
        display = false;
      } else {
        inMath = true;
        display = dbl;
        if (dbl) i++;
      }
      continue;
    }
    buf += c;
  }
  if (inMath) out.push({ math: false, display: false, text: (display ? '$$' : '$') + buf });
  else if (buf) out.push({ math: false, display: false, text: buf });
  return out;
}

/** Renders text with $…$ maths through KaTeX (DX-04). Before KaTeX loads it shows the raw text. */
export function renderMath(text: string, katex: Katex | null): string {
  return splitMath(text)
    .map((s) => {
      if (!s.math) return escapeHtml(s.text).replace(/\n/g, '<br>');
      if (!katex) return escapeHtml(s.text);
      try {
        return katex.renderToString(s.text, { displayMode: s.display, throwOnError: false, strict: 'ignore' });
      } catch {
        return `<code>${escapeHtml(s.text)}</code>`;
      }
    })
    .join('');
}

@Component({
  selector: 'app-math',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [innerHTML]="html()"></span>`,
  styles: `
    :host { overflow-wrap: anywhere; }
    :host ::ng-deep .katex { font-size: 1.08em; }
    :host ::ng-deep .katex-display { overflow-x: auto; overflow-y: hidden; padding: 4px 0; }
  `,
})
export class MathText {
  readonly text = input.required<string>();
  private readonly loader = inject(KatexLoader);
  private readonly sanitizer = inject(DomSanitizer);

  // KaTeX output is built from escaped input and is safe to trust; Angular's sanitiser would strip its MathML.
  protected readonly html = computed<SafeHtml>(() =>
    this.sanitizer.bypassSecurityTrustHtml(renderMath(this.text(), this.loader.katex())),
  );

  constructor() {
    effect(() => {
      if (/\$/.test(this.text())) void this.loader.load();
    });
  }
}
