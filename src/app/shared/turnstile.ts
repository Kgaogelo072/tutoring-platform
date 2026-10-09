import {
  ChangeDetectionStrategy,
  Component,
  DOCUMENT,
  ElementRef,
  OnDestroy,
  afterNextRender,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

interface TurnstileApi {
  render(el: HTMLElement, opts: Record<string, unknown>): string;
  reset(id: string): void;
  remove(id: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

let scriptPromise: Promise<TurnstileApi> | undefined;

function loadScript(doc: Document): Promise<TurnstileApi> {
  scriptPromise ??= new Promise((resolve, reject) => {
    const s = doc.createElement('script');
    s.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
    s.async = true;
    s.onload = () => (window.turnstile ? resolve(window.turnstile) : reject(new Error('Turnstile missing')));
    s.onerror = () => reject(new Error('Turnstile failed to load'));
    doc.head.appendChild(s);
  });
  return scriptPromise;
}

/** Cloudflare Turnstile widget (REG-06). Emits the token, or null when it expires or fails. */
@Component({
  selector: 'app-turnstile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #box></div>`,
})
export class Turnstile implements OnDestroy {
  readonly siteKey = input.required<string>();
  readonly token = output<string | null>();
  private readonly box = viewChild.required<ElementRef<HTMLElement>>('box');
  private readonly doc = inject(DOCUMENT);
  private api?: TurnstileApi;
  private widgetId?: string;

  constructor() {
    afterNextRender(async () => {
      try {
        this.api = await loadScript(this.doc);
        this.widgetId = this.api.render(this.box().nativeElement, {
          sitekey: this.siteKey(),
          size: 'flexible',
          callback: (t: string) => this.token.emit(t),
          'expired-callback': () => this.token.emit(null),
          'error-callback': () => this.token.emit(null),
        });
      } catch {
        this.token.emit(null);
      }
    });
  }

  /** Tokens are single use; call after each submit attempt. */
  reset() {
    if (this.api && this.widgetId) this.api.reset(this.widgetId);
    this.token.emit(null);
  }

  ngOnDestroy() {
    if (this.api && this.widgetId) this.api.remove(this.widgetId);
  }
}
