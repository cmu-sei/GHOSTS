import { TestBed } from '@angular/core/testing';
import { FooterComponent } from './footer.component';

describe('FooterComponent', () => {
  it('carries the standard notice, with no GHOSTS name or links', async () => {
    const fixture = TestBed.createComponent(FooterComponent);
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;

    const notices = [...root.querySelectorAll('footer span')].map(s => s.textContent?.trim());
    expect(notices).toEqual(['Crucible Scenario Builder', 'CERT © 2017. All rights reserved.']);
    expect(root.querySelectorAll('a')).toHaveLength(0);
  });
});
