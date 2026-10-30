import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Me } from '../models/builder.model';

/** Who the auth proxy says is at the keyboard, read once. Null until it answers, or if it can't. */
@Injectable({ providedIn: 'root' })
export class MeService {
  readonly me = signal<Me | null>(null);

  constructor() {
    inject(HttpClient).get<Me>('/api/me').subscribe({ next: me => this.me.set(me), error: () => this.me.set(null) });
  }
}
