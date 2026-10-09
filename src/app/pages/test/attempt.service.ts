import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AttemptStatus, QuestionType, Subject, apiUrl } from '../../core/api';

export interface AttemptState {
  status: AttemptStatus;
  learnerFirstName: string;
  subject: Subject;
  subjectName: string;
  timeLimitMinutes: number;
  questionCount: number;
  numericQuestionCount: number;
  calculatorAllowed: boolean;
  startedAt: string | null;
  deadlineAt: string | null;
  serverNow: string;
  remainingSeconds: number | null;
}

export interface QuestionDto {
  id: string;
  number: number;
  type: QuestionType;
  stemLatex: string;
  imageUrl: string | null;
  marks: number;
  unit: string | null;
  options: { id: string; textLatex: string }[];
  selectedOptionId: string | null;
  numericRaw: string | null;
}

export interface QuestionsResponse {
  remainingSeconds: number;
  deadlineAt: string;
  questions: QuestionDto[];
}

/** Public, token-authorised calls for taking a test (section 6). */
@Injectable({ providedIn: 'root' })
export class AttemptApi {
  private readonly http = inject(HttpClient);
  private url = (token: string, path = '') => apiUrl(`/attempts/${encodeURIComponent(token)}${path}`);

  state(token: string) {
    return this.http.get<AttemptState>(this.url(token));
  }

  start(token: string) {
    return this.http.post<AttemptState>(this.url(token, '/start'), null);
  }

  questions(token: string) {
    return this.http.get<QuestionsResponse>(this.url(token, '/questions'));
  }

  save(token: string, questionId: string, body: { selectedOptionId?: string | null; numericRaw?: string | null }) {
    return this.http.put<{ questionId: string; answeredAt: string | null }>(
      this.url(token, `/responses/${questionId}`),
      body,
    );
  }

  submit(token: string) {
    return this.http.post<AttemptState>(this.url(token, '/submit'), null);
  }
}
