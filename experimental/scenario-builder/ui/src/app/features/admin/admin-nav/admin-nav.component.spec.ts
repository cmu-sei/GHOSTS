import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminNavComponent } from './admin-nav.component';

describe('AdminNavComponent', () => {
  it('links to each Admin section', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AdminNavComponent);
    await fixture.whenStable();

    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a'));
    expect(links.map(a => [a.textContent?.trim(), a.getAttribute('href')])).toEqual([
      ['Scenario Types', '/admin/scenario-types'],
      ['Template Libraries', '/admin/libraries']
    ]);
  });
});
