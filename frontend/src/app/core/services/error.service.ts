import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ErrorService {
  message(error: unknown, fallback = 'Não foi possível concluir a operação.'): string {
    if (!(error instanceof HttpErrorResponse)) return fallback;
    const problem = error.error as ProblemDetails | null;
    if (problem?.errors) {
      const messages = Object.values(problem.errors).flat().filter(Boolean);
      if (messages.length) return messages.join(' ');
    }
    return problem?.detail ?? problem?.title ?? (error.status === 0 ? 'API indisponível. Verifique o backend.' : fallback);
  }
}
