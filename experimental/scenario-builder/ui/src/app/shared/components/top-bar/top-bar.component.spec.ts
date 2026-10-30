import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Me } from '../../../core/models/builder.model';
import { MeService } from '../../../core/services/me.service';
import { TopBarComponent } from './top-bar.component';

async function render(me: Me | null) {
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: MeService, useValue: { me: signal(me) } }] });
  const fixture = TestBed.createComponent(TopBarComponent);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

const adminLink = (root: HTMLElement) => Array.from(root.querySelectorAll('a')).find(a => a.textContent?.trim() === 'Admin');

describe('TopBarComponent', () => {
  it('shows Admin only to admins', async () => {
    expect(adminLink(await render({ user: 'dev', admin: true }))!.getAttribute('href')).toBe('/admin');
    TestBed.resetTestingModule();
    expect(adminLink(await render({ user: 'lee', admin: false }))).toBeUndefined();
    TestBed.resetTestingModule();
    expect(adminLink(await render(null))).toBeUndefined();
  });
});
