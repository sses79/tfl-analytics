import { Component, input, signal } from '@angular/core';

export interface DataFlowStep {
  service: string;
  detail: string;
  tone: 'source' | 'compute' | 'messaging' | 'storage' | 'api' | 'ui';
}

@Component({
  selector: 'app-data-flow-explainer',
  templateUrl: './data-flow-explainer.component.html',
  styleUrl: './data-flow-explainer.component.scss'
})
export class DataFlowExplainerComponent {
  readonly title = input.required<string>();
  readonly description = input.required<string>();
  readonly eventType = input.required<string>();
  readonly steps = input.required<readonly DataFlowStep[]>();

  protected readonly expanded = signal(false);

  protected toggle(): void {
    this.expanded.update(value => !value);
  }
}
