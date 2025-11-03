import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CreateLocalTimelineRequest, LocalTimeline, TimelineHandler } from '../models';

const STORAGE_KEY = 'ghosts-local-timelines';

@Injectable({
  providedIn: 'root'
})
export class TimelineLocalService {
  private readonly snackBar = inject(MatSnackBar);
  private cache: LocalTimeline[] | null = null;

  getAll(): LocalTimeline[] {
    return this.clone(this.load());
  }

  getById(id: string): LocalTimeline | undefined {
    return this.clone(this.load().find(timeline => timeline.id === id));
  }

  create(request: CreateLocalTimelineRequest): LocalTimeline {
    const timelines = this.load();
    const globalCrypto = (globalThis as typeof globalThis & { crypto?: Crypto }).crypto;
    const timeline: LocalTimeline = {
      id: globalCrypto && typeof globalCrypto.randomUUID === 'function'
        ? globalCrypto.randomUUID()
        : `local-${Date.now()}-${Math.random().toString(16).slice(2)}`,
      name: request.name,
      timeLineHandlers: this.cloneHandlers(request.timeLineHandlers),
      status: 'Stop'
    };
    timelines.push(timeline);
    this.persist(timelines);
    this.openSnackBar(`Timeline "${timeline.name}" created`);
    return this.clone(timeline);
  }

  update(id: string, request: CreateLocalTimelineRequest): LocalTimeline {
    const timelines = this.load();
    const index = timelines.findIndex(timeline => timeline.id === id);
    if (index === -1) {
      throw new Error('Timeline not found');
    }
    const updated: LocalTimeline = {
      ...timelines[index],
      name: request.name,
      timeLineHandlers: this.cloneHandlers(request.timeLineHandlers)
    };
    timelines[index] = updated;
    this.persist(timelines);
    this.openSnackBar(`Timeline "${updated.name}" updated`);
    return this.clone(updated);
  }

  delete(id: string): void {
    const timelines = this.load();
    const index = timelines.findIndex(timeline => timeline.id === id);
    if (index === -1) {
      return;
    }
    const [removed] = timelines.splice(index, 1);
    this.persist(timelines);
    this.openSnackBar(`Timeline "${removed.name}" deleted`);
  }

  private load(): LocalTimeline[] {
    if (this.cache) {
      return this.cache;
    }

    if (typeof localStorage === 'undefined') {
      this.cache = [];
      return this.cache;
    }

    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      this.cache = [];
      return this.cache;
    }

    try {
      const parsed = JSON.parse(raw) as LocalTimeline[];
      this.cache = Array.isArray(parsed) ? parsed : [];
    } catch {
      this.cache = [];
    }

    return this.cache!;
  }

  private persist(timelines: LocalTimeline[]): void {
    this.cache = timelines;
    if (typeof localStorage === 'undefined') {
      return;
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(timelines));
  }

  private clone<T>(value: T): T {
    if (value === undefined || value === null) {
      return value;
    }
    if ('structuredClone' in globalThis) {
      const cloneFn = (globalThis as typeof globalThis & { structuredClone?: <K>(data: K) => K }).structuredClone;
      if (cloneFn) {
        return cloneFn(value);
      }
    }
    return JSON.parse(JSON.stringify(value));
  }

  private cloneHandlers(handlers: TimelineHandler[]): TimelineHandler[] {
    return handlers ? this.clone(handlers) : [];
  }

  private openSnackBar(message: string): void {
    this.snackBar.open(message, 'Close', {
      duration: 3000,
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
  }
}
