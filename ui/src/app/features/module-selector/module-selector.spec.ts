import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ModuleSelector } from './module-selector';

describe('ModuleSelector', () => {
  let component: ModuleSelector;
  let fixture: ComponentFixture<ModuleSelector>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ModuleSelector],
    }).compileComponents();

    fixture = TestBed.createComponent(ModuleSelector);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
