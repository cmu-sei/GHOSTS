import { ChangeDetectionStrategy, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NpcChatAction, NpcChatMessage } from '../../../core/models';
import { NpcChatService } from '../../../core/services';

export interface NpcChatDialogData {
  npcId: string;
  npcName: string;
  firstName: string;
  photoUrl: string;
}

interface ChatEntry extends NpcChatMessage {
  actions?: NpcChatAction[];
}

@Component({
  selector: 'app-npc-chat-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title>
      <img [src]="data.photoUrl" alt="" class="avatar">
      <span>{{ data.npcName }}</span>
    </h2>

    <mat-dialog-content>
      <div class="controls">
        <span class="mode-label">Talk to {{ data.firstName }}</span>

        <mat-form-field class="model-field" subscriptSizing="dynamic">
          <mat-label>Model</mat-label>
          <mat-select [(ngModel)]="model" [disabled]="models().length === 0">
            @for (m of models(); track m) {
              <mat-option [value]="m">{{ m }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      @if (error()) {
        <div class="error-message">
          <i class="fas fa-exclamation-circle"></i>
          <span>{{ error() }}</span>
        </div>
      }

      <div class="thread" #thread>
        @if (entries().length === 0) {
          <p class="hint">
            Talk to {{ data.firstName }} directly. Ask what they do, what they have been up to,
            or tell them to do something &mdash; "go read the news on cnn.com" or
            "post something to Facebook" is carried out and added to their history.
          </p>
        }

        @for (entry of entries(); track $index) {
          <div class="bubble" [class.user]="entry.role === 'user'">
            <div class="who">{{ entry.role === 'user' ? 'You' : data.firstName }}</div>
            <div class="text">{{ entry.content }}</div>
            @for (action of entry.actions; track $index) {
              <div class="action" [class.failed]="!action.ok">
                <i class="fas" [class.fa-bolt]="action.ok" [class.fa-triangle-exclamation]="!action.ok"></i>
                <span>{{ action.tool }}{{ action.argument ? ' · ' + action.argument : '' }}</span>
                @if (action.detail) {
                  <span class="detail">{{ action.detail }}</span>
                }
              </div>
            }
          </div>
        }

        @if (sending()) {
          <div class="bubble pending">
            <mat-spinner diameter="18"></mat-spinner>
            <span>thinking&hellip;</span>
          </div>
        }
      </div>
    </mat-dialog-content>

    <mat-dialog-actions>
      <mat-form-field class="message-field" subscriptSizing="dynamic">
        <mat-label>Say something</mat-label>
        <textarea
          matInput
          rows="2"
          [(ngModel)]="message"
          (keydown.enter)="onEnter($event)"
          [disabled]="sending()"
          placeholder="Enter to send, Shift+Enter for a new line"></textarea>
      </mat-form-field>
      <button mat-raised-button color="primary" (click)="send()" [disabled]="sending() || !message.trim()">
        <i class="fas fa-paper-plane"></i>
        Send
      </button>
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: [`
    h2[mat-dialog-title] {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      object-fit: cover;
    }

    mat-dialog-content {
      display: flex;
      flex-direction: column;
      gap: 12px;
      min-height: 340px;
    }

    .controls {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }

    .mode-label {
      font-size: 14px;
      font-weight: 500;
    }

    .model-field {
      min-width: 200px;
    }

    .error-message {
      display: flex;
      align-items: center;
      gap: 8px;
      color: #f44336;
      font-size: 13px;
    }

    .thread {
      flex: 1 1 auto;
      overflow-y: auto;
      max-height: 46vh;
      display: flex;
      flex-direction: column;
      gap: 12px;
      padding: 4px;
    }

    .hint {
      color: rgba(0, 0, 0, 0.6);
      font-size: 13px;
      line-height: 1.5;
      margin: 0;
    }

    .bubble {
      align-self: flex-start;
      max-width: 85%;
      background: #f1f3f6;
      border-radius: 10px;
      padding: 8px 12px;
    }

    .bubble.user {
      align-self: flex-end;
      background: #e3f0ff;
    }

    .bubble.pending {
      display: flex;
      align-items: center;
      gap: 8px;
      color: rgba(0, 0, 0, 0.6);
      font-size: 13px;
    }

    .who {
      font-size: 11px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: rgba(0, 0, 0, 0.5);
      margin-bottom: 2px;
    }

    .text {
      white-space: pre-wrap;
      font-size: 14px;
      line-height: 1.45;
    }

    .action {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
      margin-top: 8px;
      padding-top: 6px;
      border-top: 1px solid rgba(0, 0, 0, 0.08);
      font-family: 'Fira Code', 'Menlo', monospace;
      font-size: 11px;
      color: #2e7d32;
    }

    .action.failed {
      color: #b71c1c;
    }

    .action .detail {
      color: rgba(0, 0, 0, 0.5);
    }

    mat-dialog-actions {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 8px 24px 20px;
    }

    .message-field {
      flex: 1 1 auto;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NpcChatDialogComponent implements OnInit {
  protected readonly data = inject<NpcChatDialogData>(MAT_DIALOG_DATA);
  private readonly chatService = inject(NpcChatService);

  @ViewChild('thread') private thread?: ElementRef<HTMLDivElement>;

  protected readonly entries = signal<ChatEntry[]>([]);
  protected readonly models = signal<string[]>([]);
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected message = '';
  protected model = '';

  ngOnInit(): void {
    this.chatService.getConfig().subscribe({
      next: (config) => {
        this.models.set(config.availableModels ?? []);
        this.model = config.model;
        if (!config.isReachable) {
          this.error.set(`Cannot reach the chat model host at ${config.host}: ${config.error ?? 'unknown error'}`);
        }
      },
      error: (err) => this.error.set(err.error?.error ?? err.message ?? 'Failed to load chat configuration')
    });
  }

  protected onEnter(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    if (keyboardEvent.shiftKey) {
      return;
    }

    keyboardEvent.preventDefault();
    this.send();
  }

  protected send(): void {
    const message = this.message.trim();
    if (!message || this.sending()) {
      return;
    }

    const history = this.entries().map(({ role, content }) => ({ role, content }));

    this.entries.update(entries => [...entries, { role: 'user', content: message }]);
    this.message = '';
    this.error.set(null);
    this.sending.set(true);
    this.scrollToBottom();

    this.chatService.chat(this.data.npcId, {
      message,
      history,
      mode: 'as',
      model: this.model || undefined
    }).subscribe({
      next: (response) => {
        this.entries.update(entries => [...entries, {
          role: 'npc',
          content: response.reply,
          actions: response.actions ?? undefined
        }]);
        this.sending.set(false);
        this.scrollToBottom();
      },
      error: (err) => {
        this.error.set(err.error?.error ?? err.message ?? 'Chat request failed');
        this.sending.set(false);
      }
    });
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const element = this.thread?.nativeElement;
      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }
}
