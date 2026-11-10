import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from './shared/components/footer/footer.component';
import { GuideComponent } from './shared/help/guide/guide.component';
import { TourComponent } from './shared/help/tour/tour.component';

/**
 * Each page draws its own top bar: the list has the product's, a scenario adds its name and pages. The footer is under every page;
 * the Guide and the tour sit over all of them.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, FooterComponent, GuideComponent, TourComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {}
