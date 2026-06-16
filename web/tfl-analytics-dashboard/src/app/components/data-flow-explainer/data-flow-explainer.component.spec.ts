import { ComponentFixture, TestBed } from '@angular/core/testing';
import {
  DataFlowExplainerComponent,
  DataFlowStep
} from './data-flow-explainer.component';

describe('DataFlowExplainerComponent', () => {
  let fixture: ComponentFixture<DataFlowExplainerComponent>;

  const steps: readonly DataFlowStep[] = [
    { service: 'Event Hubs', detail: 'Event stream', tone: 'messaging' },
    { service: 'Cosmos DB', detail: 'Live state', tone: 'storage' }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DataFlowExplainerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(DataFlowExplainerComponent);
    fixture.componentRef.setInput('title', 'Arrival flow');
    fixture.componentRef.setInput('description', 'Explains the event path.');
    fixture.componentRef.setInput('eventType', 'ArrivalObserved');
    fixture.componentRef.setInput('steps', steps);
    fixture.detectChanges();
  });

  it('shows the flow diagram by default and allows hiding it', () => {
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelectorAll('.flow-node')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('ArrivalObserved');

    button.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('.flow-panel')).toBeNull();

    button.click();
    fixture.detectChanges();

    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelectorAll('.flow-node')).toHaveLength(2);
  });
});
