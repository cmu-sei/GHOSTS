import { Component, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-search-bar',
  standalone: true,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule
  ],
  template: `
    <mat-form-field class="search-field" appearance="outline">
      <mat-label>Search</mat-label>
      <i class="fas fa-search search-icon" matPrefix></i>
      <input
        matInput
        type="text"
        [(ngModel)]="searchTerm"
        (ngModelChange)="onSearchChange($event)"
        placeholder="Type to search..."
        autocomplete="off">
      @if (searchTerm()) {
        <button
          matSuffix
          mat-icon-button
          aria-label="Clear search"
          (click)="clearSearch()">
          <i class="fas fa-times"></i>
        </button>
      }
    </mat-form-field>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      max-width: 400px;
    }

    .search-field {
      width: 100%;
    }

    .search-field ::ng-deep .mat-mdc-text-field-wrapper {
      padding-bottom: 0;
    }

    .search-field ::ng-deep .mat-mdc-form-field-subscript-wrapper {
      display: none;
    }

    .search-field ::ng-deep .mat-mdc-form-field-infix {
      padding-top: 12px;
      padding-bottom: 12px;
      min-height: 40px;
      display: flex;
      align-items: center;
    }

    .search-field ::ng-deep .mat-mdc-input-element {
      align-self: center;
    }

    .search-field ::ng-deep .mat-mdc-floating-label {
      top: 24px !important;
    }

    .search-icon {
      color: rgba(0, 0, 0, 0.54);
      margin-right: 8px;
      margin-left: 8px;
      font-size: 16px;
      align-self: center;
    }

    button i {
      font-size: 14px;
    }
  `]
})
export class SearchBarComponent {
  protected readonly searchTerm = signal('');
  readonly searchChange = output<string>();

  private debounceTimer: any;

  protected onSearchChange(value: string): void {
    clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => {
      this.searchChange.emit(value);
    }, 300);
  }

  protected clearSearch(): void {
    this.searchTerm.set('');
    this.searchChange.emit('');
  }
}
