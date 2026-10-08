/**
 * All landing-page copy and business settings live here (requirement LP-06).
 * Edit this file to change text, prices or contact details — no component changes needed.
 * Values marked TODO must be filled in before going live.
 */

export const site = {
  brand: 'Engineered Tutors',
  tagline: 'Grade 12 · Maths & Physical Sciences',
  domain: 'https://www.engineeredtutors.co.za', // TODO: confirm domain
  email: 'hello@engineeredtutors.co.za', // TODO: confirm mailbox
  /** International format, digits only, e.g. 27821234567 */
  whatsappNumber: '27000000000', // TODO: your WhatsApp Business number
  whatsappMessage: "Hi! I'd like to know more about the 2027 Matric Accelerator.",
  diagnosticOpens: '8 November 2026',
  foundingDeadline: '15 December 2026',
  cohortSize: 15,
  registrationFee: 250,
  tutor: {
    name: 'Kgaogelo', // TODO: add surname if you want it public
    photo: '', // TODO: path to a real photo of you, e.g. /img/tutor.webp
  },
};

export const hero = {
  eyebrow: 'Online Grade 12 coaching · 2027 founding cohort',
  titleLead: 'Maths & Physical Sciences,',
  titleAccent: 'taught by an engineer.',
  subtitle:
    'Find out exactly where your child is losing marks before Grade 12 starts — then close those gaps in small, live online classes with monthly progress reports for parents.',
  primaryCta: 'Take the free diagnostic',
  secondaryCta: 'See the programme',
  highlights: [
    { icon: 'live', title: 'Live online classes', text: 'Small groups, max 15' },
    { icon: 'exam', title: 'Exam-focused', text: 'Past papers & technique' },
    { icon: 'chart', title: 'Progress tracking', text: 'Monthly parent reports' },
    { icon: 'cap', title: 'University ready', text: 'Engineering, science, tech' },
  ],
};

export const problem = {
  eyebrow: 'Why marks drop in Grade 12',
  title: 'Most Grade 12 marks are lost on Grade 10 and 11 gaps.',
  text:
    "Weak algebra makes calculus feel impossible. Shaky trig costs marks in both Maths and Physics. Knowing a formula isn't the same as knowing when to use it. Extra homework help doesn't fix the foundation underneath — finding the gaps first does.",
  points: [
    'Algebra and equation-rearranging that leak marks in Physics',
    'Functions and graphs — heavily weighted in Paper 1',
    'Trigonometry, the most common weak area',
    "Newton's laws and free-body diagrams",
    'Stoichiometry and chemistry in Paper 2',
  ],
};

export const steps = [
  {
    title: 'Take the free diagnostic',
    text: 'A 45-minute online test per subject, done on a phone or laptop. It covers the Grade 10–11 work Grade 12 builds on.',
  },
  {
    title: 'Get a personal report',
    text: "Within 48 hours, parents receive a one-page report: strong topics, developing topics, and the priority gaps.",
  },
  {
    title: 'Join live classes',
    text: 'Two evening classes a week per subject, weekly problem sets with feedback, and a monthly test.',
  },
  {
    title: 'Track the progress',
    text: 'Every month, parents get a short report showing marks by topic and what we focus on next.',
  },
];

export const subjects = [
  {
    name: 'Mathematics',
    image: 'maths-notebook',
    alt: 'Open notebook with handwritten algebra, trigonometry and calculus notes',
    text: 'Algebra, functions and graphs, sequences, calculus, trigonometry, analytical and Euclidean geometry, finance and probability.',
    schedule: 'Mon & Wed · 18:00–19:15',
  },
  {
    name: 'Physical Sciences',
    image: 'lab-desk',
    alt: 'Desk with physics equipment, a circuit board and handwritten notes',
    text: "Newton's laws, momentum, work and energy, electricity, waves — plus the full Paper 2 chemistry: stoichiometry, rates, equilibrium, acids and bases, electrochemistry and organic.",
    schedule: 'Tue & Thu · 18:00–19:15',
  },
];

export const pricing = {
  eyebrow: '2027 Matric Accelerator',
  title: 'Founding cohort pricing',
  intro:
    'Founding prices are locked in for all of 2027. Founding spots close on {deadline} or when {cohort} spots are filled.',
  plans: [
    {
      name: 'Mathematics',
      price: 599,
      standard: 699,
      featured: false,
      features: ['2 live classes a week', 'Weekly problem set with feedback', 'Monthly test', 'Recorded lessons', 'Monthly parent report'],
    },
    {
      name: 'Maths + Physical Sciences',
      price: 799,
      standard: 999,
      featured: true,
      features: ['Everything in both subjects', '4 live classes a week', 'Monthly past-paper clinic', 'Weekly question window on WhatsApp', 'Monthly parent report'],
    },
    {
      name: 'Physical Sciences',
      price: 599,
      standard: 699,
      featured: false,
      features: ['2 live classes a week', 'Weekly problem set with feedback', 'Monthly test', 'Recorded lessons', 'Monthly parent report'],
    },
  ],
  feeNote:
    'A once-off R{fee} registration fee secures a founding spot and is credited to your January fee. It is refunded in full if the cohort does not run.',
};

export const about = {
  eyebrow: 'Who teaches',
  title: "Learn from someone who uses this maths and physics.",
  paragraphs: [
    "I'm {name}, a Mechanical Engineering student at Wits and a working software developer. Every day I use the algebra, calculus, mechanics and problem-solving that Grade 12 is built on.",
    "I don't teach learners to memorise formulas. I teach them how an engineer approaches a problem — understand what's happening, choose the right tool, and check that the answer makes sense.",
  ],
};

export const faqs = [
  {
    q: 'Who is this for?',
    a: 'Learners going into Grade 12 in 2027 who take CAPS Mathematics and/or Physical Sciences — especially those sitting around 45–70% who want to move into the 60–80%+ range, and stronger learners aiming for engineering, science or other technical degrees.',
  },
  {
    q: 'Is the diagnostic really free?',
    a: "Yes. There's no cost and no obligation. You get a report either way, and you decide afterwards whether the programme is a good fit.",
  },
  {
    q: 'Are the classes online?',
    a: 'Yes. Classes are live on Google Meet in the evenings, so learners can join from anywhere in South Africa on a laptop, tablet or phone.',
  },
  {
    q: 'What if my child misses a class?',
    a: 'Every class is recorded, and the weekly problem set covers the same work, so missed lessons can be caught up.',
  },
  {
    q: 'How big are the classes?',
    a: 'The founding cohort is capped at 15 learners, so everyone gets attention and feedback.',
  },
  {
    q: 'How do payments work?',
    a: 'A R{fee} registration fee secures the spot, then a monthly debit order from January. You can cancel with one calendar month\'s notice.',
  },
  {
    q: 'What happens with my child\'s information?',
    a: "We collect only what we need to run the diagnostic, classes and progress reports, store it securely in South Africa, and never sell or share it. See our privacy policy for details.",
  },
];

export const finalCta = {
  title: 'Find the gaps before Grade 12 does.',
  text: 'The diagnostic takes about 45 minutes per subject. Your report arrives within 48 hours.',
  cta: 'Take the free diagnostic',
};

/** Replaces {name}, {fee}, {deadline}, {cohort} placeholders in copy. */
export function fill(text: string): string {
  return text
    .replace('{name}', site.tutor.name)
    .replace('{fee}', String(site.registrationFee))
    .replace('{deadline}', site.foundingDeadline)
    .replace('{cohort}', String(site.cohortSize));
}

export const whatsappLink = () =>
  `https://wa.me/${site.whatsappNumber}?text=${encodeURIComponent(site.whatsappMessage)}`;
