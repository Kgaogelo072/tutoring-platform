import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AttemptStatus,
  Band,
  IdType,
  LeadStatus,
  QuestionType,
  Subject,
  apiUrl,
} from '../core/api';
import { QuestionDto } from '../pages/test/attempt.service';

export interface LeadTest {
  subject: Subject;
  status: AttemptStatus;
  overallPct: number | null;
  submittedAt: string | null;
}

export interface LeadLearner {
  id: string;
  name: string;
  grade: number;
  school: string;
  idMasked: string | null;
  tests: LeadTest[];
}

export interface Lead {
  id: string;
  parentId: string;
  parentName: string;
  phone: string;
  email: string;
  source: string;
  registeredAt: string;
  status: LeadStatus;
  notes: string;
  updatedAt: string;
  bestPct: number | null;
  learners: LeadLearner[];
}

export interface TopicScore {
  code: string;
  name: string;
  marksEarned: number;
  marksAvailable: number;
  pct: number;
  band: Band;
}

export interface AnswerRow {
  number: number;
  code: string;
  section: string;
  topic: string;
  type: QuestionType;
  stemLatex: string;
  imageUrl: string | null;
  marks: number;
  answerGiven: string | null;
  correctAnswer: string;
  isCorrect: boolean | null;
  marksAwarded: number | null;
  answeredAt: string | null;
  explanation: string;
}

export interface AttemptDetail {
  id: string;
  subject: Subject;
  assessmentVersion: number;
  status: AttemptStatus;
  createdAt: string;
  tokenExpiresAt: string;
  startedAt: string | null;
  deadlineAt: string | null;
  submittedAt: string | null;
  timeTakenSeconds: number | null;
  overallPct: number | null;
  topics: TopicScore[];
  questions: AnswerRow[];
}

export interface LearnerDetail {
  learner: {
    id: string;
    firstName: string;
    surname: string;
    school: string;
    grade: number;
    goal: string;
    idType: IdType | null;
    idMasked: string | null;
    dateOfBirth: string | null;
    createdAt: string;
    subjects: { subject: Subject; latestMark: number | null }[];
  };
  parent: { id: string; fullName: string; phone: string; email: string; source: string; createdAt: string };
  lead: { id: string; status: LeadStatus; notes: string; updatedAt: string } | null;
  consents: { textVersion: string; acceptedAt: string; ipAddress: string }[];
  attempts: AttemptDetail[];
}

export interface AssessmentSummary {
  id: string;
  subject: Subject;
  version: number;
  timeLimitMinutes: number;
  createdAt: string;
  publishedAt: string | null;
  questionCount: number;
  totalMarks: number;
  attemptCount: number;
}

export interface ImportResult {
  assessmentId: string;
  subject: Subject;
  version: number;
  questionCount: number;
  totalMarks: number;
}

export interface ImportError {
  row: number;
  column: string;
  message: string;
}

export interface Preview {
  id: string;
  subject: Subject;
  subjectName: string;
  version: number;
  timeLimitMinutes: number;
  publishedAt: string | null;
  questions: QuestionDto[];
}

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  leads(q: { search?: string; status?: LeadStatus | ''; sort?: string; dir?: 'asc' | 'desc' }) {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(q)) if (v) params = params.set(k, v);
    return this.http.get<Lead[]>(apiUrl('/admin/leads'), { params });
  }

  updateLead(id: string, body: { status?: LeadStatus; notes?: string }) {
    return this.http.patch<{ id: string; status: LeadStatus; notes: string; updatedAt: string }>(
      apiUrl(`/admin/leads/${id}`),
      body,
    );
  }

  learner(id: string) {
    return this.http.get<LearnerDetail>(apiUrl(`/admin/learners/${id}`));
  }

  assessments() {
    return this.http.get<AssessmentSummary[]>(apiUrl('/admin/assessments'));
  }

  importQuestions(file: File, subject: Subject, timeLimitMinutes: number) {
    const form = new FormData();
    form.append('file', file);
    form.append('subject', subject);
    form.append('timeLimitMinutes', String(timeLimitMinutes));
    return this.http.post<ImportResult>(apiUrl('/admin/questions/import'), form);
  }

  updateAssessment(id: string, timeLimitMinutes: number) {
    return this.http.patch(apiUrl(`/admin/assessments/${id}`), { timeLimitMinutes });
  }

  publish(id: string) {
    return this.http.post(apiUrl(`/admin/assessments/${id}/publish`), null);
  }

  preview(id: string) {
    return this.http.get<Preview>(apiUrl(`/admin/assessments/${id}/preview`));
  }

  exportLeads() {
    return this.http.get(apiUrl('/admin/export/leads.csv'), { responseType: 'blob', observe: 'response' });
  }
}

/** AD-05: wa.me link with a pre-filled message to the parent. */
export function whatsappToParent(phone: string, parentName: string, learnerName?: string): string {
  const first = parentName.split(' ')[0];
  const about = learnerName ? `${learnerName}'s` : 'the';
  const text = `Hi ${first}, this is Kgaogelo from Engineered Tutors about ${about} diagnostic results.`;
  return `https://wa.me/${phone.replace(/\D/g, '')}?text=${encodeURIComponent(text)}`;
}
