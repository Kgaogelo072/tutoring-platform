import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { site, whatsappLink } from '../content/site-content';

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <div class="container grid">
        <div>
          <div class="brand">
            <img src="img/icon-180.png" width="44" height="44" alt="" />
            <div>
              <strong>{{ site.brand }}</strong>
              <span>{{ site.tagline }}</span>
            </div>
          </div>
          <p class="blurb">Online CAPS Grade 12 coaching for learners across South Africa.</p>
        </div>
        <nav aria-label="Footer">
          <a routerLink="/diagnostic">Free diagnostic</a>
          <a routerLink="/" fragment="pricing">Pricing</a>
          <a routerLink="/" fragment="faq">FAQ</a>
          <a routerLink="/privacy">Privacy policy</a>
        </nav>
        <div class="contact">
          <a [href]="wa" target="_blank" rel="noopener">WhatsApp us</a>
          <a [href]="'mailto:' + site.email">{{ site.email }}</a>
        </div>
      </div>
      <div class="container legal">© {{ year }} {{ site.brand }}. All rights reserved.</div>
    </footer>
  `,
  styles: `
    .footer { background: var(--ink); color: #c9cfdb; padding: 56px 0 28px; font-size: 0.95rem; }
    .grid { display: grid; gap: 32px; }
    .brand { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }
    .brand strong { display: block; font-family: var(--font-display); color: #fff; font-size: 1.3rem; }
    .brand span { display: block; color: var(--gold-2); font-size: 0.82rem; }
    .blurb { max-width: 34ch; margin: 0; }
    nav, .contact { display: flex; flex-direction: column; gap: 10px; }
    a { color: #e6e9f0; text-decoration: none; padding: 2px 0; }
    a:hover { color: var(--gold-2); }
    .legal { margin-top: 40px; padding-top: 20px; border-top: 1px solid rgba(255,255,255,0.1); font-size: 0.85rem; color: #8b93a3; }
    @media (min-width: 768px) { .grid { grid-template-columns: 2fr 1fr 1fr; } }
  `,
})
export class SiteFooter {
  site = site;
  wa = whatsappLink();
  year = new Date().getFullYear();
}
