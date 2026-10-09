import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { site, whatsappLink } from '../../content/site-content';
import { WhatsappIcon } from '../../shared/icon';

/**
 * Placeholder for the registration + diagnostic flow (REG / DX requirements).
 * Replace with the registration form once the API is ready.
 */
@Component({
  selector: 'app-diagnostic',
  imports: [RouterLink, WhatsappIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="section">
      <div class="container narrow">
        <span class="eyebrow">Matric Readiness Diagnostic</span>
        <h1>The free diagnostic opens on {{ site.diagnosticOpens }}.</h1>
        <p class="lead">
          It's a 40–45-minute online test per subject, with multiple-choice and number answers, built on the Grade 10–11 work that Grade 12 depends on.
          Parents receive a one-page report within 48 hours showing strong topics, developing topics and the priority gaps.
        </p>
        <div class="card">
          <h2>Want a spot when it opens?</h2>
          <p>Send us a WhatsApp and we'll send you the link on launch day.</p>
          <a class="btn btn-wa" [href]="wa" target="_blank" rel="noopener">
            <app-whatsapp-icon [size]="22" /> Message us on WhatsApp
          </a>
        </div>
        <p><a routerLink="/">← Back to home</a></p>
      </div>
    </section>
  `,
  styles: `
    .narrow { max-width: 760px; }
    h1 { font-size: clamp(2rem, 5vw, 3rem); }
    .card {
      margin: 32px 0; padding: 28px; border-radius: var(--radius-lg);
      background: var(--paper); border: 1px solid var(--line); box-shadow: var(--shadow);
    }
    .card h2 { font-size: 1.5rem; }
    .btn-wa { background: #25d366; color: #fff; }
  `,
})
export class Diagnostic {
  site = site;
  wa = whatsappLink();
  constructor() {
    inject(Title).setTitle('Free Matric Readiness Diagnostic | Engineered Tutors');
  }
}
