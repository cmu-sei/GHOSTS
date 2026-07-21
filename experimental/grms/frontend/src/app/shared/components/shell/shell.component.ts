import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { StatusBadgeComponent } from '../status-badge/status-badge.component';
import { FooterComponent } from '../footer/footer.component';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive,
    MatToolbarModule, MatIconModule, MatButtonModule,
    StatusBadgeComponent, FooterComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  navItems: NavItem[] = [
    { path: '/', label: 'Dashboard', icon: 'dashboard' },
    { path: '/leaders', label: 'Leaders', icon: 'person' },
    { path: '/populations', label: 'Populations', icon: 'groups' },
    { path: '/predictions', label: 'Predictions', icon: 'psychology' },
    { path: '/scenarios', label: 'Scenarios', icon: 'bolt' },
    { path: '/cognitive-lab', label: 'Cognitive Lab', icon: 'science' },
  ];
}
