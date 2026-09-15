import { TestBed } from '@angular/core/testing';
import { SidebarMenu } from './sidebar-menu';

describe('SidebarMenu', () => {
  let service: SidebarMenu;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(SidebarMenu);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
