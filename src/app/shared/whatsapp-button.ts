import { ChangeDetectionStrategy, Component } from '@angular/core';
import { whatsappLink } from '../content/site-content';
import { WhatsappIcon } from './icon';

/** Floating WhatsApp click-to-chat button (requirement LP-03). */
@Component({
  selector: 'app-whatsapp-button',
  imports: [WhatsappIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="wa" [href]="href" target="_blank" rel="noopener" aria-label="Chat to us on WhatsApp">
      <app-whatsapp-icon [size]="30" />
    </a>
  `,
  styles: `
    .wa {
      position: fixed; right: 16px; bottom: 16px; z-index: 60;
      width: 58px; height: 58px; border-radius: 50%;
      display: flex; align-items: center; justify-content: center;
      background: #25d366; color: #fff;
      box-shadow: 0 10px 24px rgba(0, 0, 0, 0.25);
      transition: transform 0.15s ease;
    }
    .wa:hover { transform: scale(1.06); }
  `,
})
export class WhatsappButton {
  href = whatsappLink();
}
