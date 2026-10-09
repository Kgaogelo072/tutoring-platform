import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { site } from '../../content/site-content';

/**
 * DRAFT privacy policy (requirement LP-05). Have it reviewed before collecting real data.
 * Bump `version` whenever the wording changes — registrations store which version was accepted (REG-05).
 */
@Component({
  selector: 'app-privacy',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="section">
      <div class="container prose">
        <span class="eyebrow">Version {{ version }} · {{ updated }}</span>
        <h1>Privacy policy</h1>
        <p>
          {{ site.brand }} ("we") provides online tutoring to Grade 12 learners. This policy explains how we handle
          personal information in line with the Protection of Personal Information Act, 2013 (POPIA).
          Because most of our learners are under 18, we only collect a learner's information with the consent of a
          parent or legal guardian.
        </p>

        <h2>What we collect</h2>
        <ul>
          <li><strong>Parent or guardian:</strong> name, cellphone number, email address, and how you heard about us.</li>
          <li><strong>Learner:</strong> name, school, grade, subjects, recent marks and goals, and, if you choose to give it, the learner's ID number (or passport number).</li>
          <li><strong>Assessment data:</strong> diagnostic and test answers, scores by topic, and time taken.</li>
          <li><strong>Consent record:</strong> when consent was given, the version of this policy, and the IP address used.</li>
        </ul>

        <h2>Why we collect it</h2>
        <ul>
          <li>To run the diagnostic and prepare a report for the parent.</li>
          <li>To identify each learner correctly, so their results are never mixed up with another learner's and they
            are not registered twice. This is the only reason we ask for an ID number, and giving it is optional.</li>
          <li>To contact the parent about the report and our programme.</li>
          <li>For enrolled learners: to run classes, track progress and send progress reports.</li>
          <li>To process registration fees and monthly payments.</li>
        </ul>
        <p>We do not sell personal information, and we do not use it for anything other than the purposes above.</p>

        <h2>Who we share it with</h2>
        <p>
          We use trusted service providers to operate: hosting (Microsoft Azure, in its South Africa region), email
          delivery, video classes, and payments (PayFast). They process information only on our instructions.
          We never share a learner's results with anyone except their parent or guardian.
        </p>

        <h2>How long we keep it</h2>
        <p>
          If a learner does not enrol, we delete their information 12 months after the diagnostic. For enrolled
          learners, we keep records for the duration of the programme and 12 months afterwards, unless the law
          requires us to keep them longer.
        </p>

        <h2>Your rights</h2>
        <p>
          You may ask to see the information we hold about you or your child, ask us to correct it, or ask us to
          delete it, at any time. You may also withdraw consent, which means we will stop processing the
          learner's information. Contact us at <a [href]="'mailto:' + site.email">{{ site.email }}</a>.
        </p>
        <p>
          If you are not satisfied with how we handle a request, you may complain to the Information Regulator
          (South Africa).
        </p>

        <h2>Security</h2>
        <p>
          Information is stored in encrypted databases hosted in South Africa, and only the tutor can access learner
          results. ID numbers are encrypted separately, are never included in emails or links, and are shown only in
          masked form (the last four digits).
        </p>

        <h2>Contact</h2>
        <p>
          Information Officer: {{ site.tutor.name }}<br />
          Email: <a [href]="'mailto:' + site.email">{{ site.email }}</a>
        </p>
      </div>
    </article>
  `,
  styles: `
    .prose { max-width: 760px; }
    .prose h1 { font-size: clamp(2rem, 5vw, 3rem); }
    .prose h2 { font-size: 1.45rem; margin-top: 1.8em; }
    .prose ul { padding-left: 1.2em; }
    .prose li { margin-bottom: 0.5em; }
  `,
})
export class Privacy {
  static readonly version = '0.3-draft';
  version = Privacy.version;
  updated = 'October 2026';
  site = site;
  constructor() {
    inject(Title).setTitle('Privacy policy | Engineered Tutors');
  }
}
