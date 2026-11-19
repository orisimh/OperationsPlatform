import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { OperationDefinition, OperationExecutionResponse, InputConfig } from '../models/operation.models';
import { OperationsApiService } from '../services/operations-api.service';

@Component({ selector: 'app-execute', templateUrl: './execute.component.html', styleUrls: ['./execute.component.scss'] })
export class ExecuteComponent implements OnInit {
  operation: OperationDefinition | null = null;
  inputA: InputConfig = {};
  inputB: InputConfig = {};
  form: FormGroup;
  response: OperationExecutionResponse | null = null;
  loading = true;
  executing = false;
  loadError = '';

  constructor(private fb: FormBuilder, private route: ActivatedRoute, private api: OperationsApiService) {
    this.form = this.fb.group({ fieldA: ['', Validators.required], fieldB: ['', Validators.required] });
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.getOperation(id).subscribe(operation => {
      this.operation = operation;
      try { this.inputA = JSON.parse(operation.inputAConfigJson || '{}'); } catch { this.inputA = {}; }
      try { this.inputB = JSON.parse(operation.inputBConfigJson || '{}'); } catch { this.inputB = {}; }
      this.form = this.fb.group({
        fieldA: ['', this.inputA.required === false ? [] : [Validators.required]],
        fieldB: ['', this.inputB.required === false ? [] : [Validators.required]]
      });
      this.loading = false;
    }, () => { this.loadError = 'Operation could not be loaded.'; this.loading = false; });
  }

  run(): void {
    if (!this.operation || this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.executing = true; this.response = null;
    this.api.execute(this.operation.id, String(this.form.value.fieldA ?? ''), String(this.form.value.fieldB ?? '')).subscribe(result => {
      this.response = result; this.executing = false;
    }, error => { this.executing = false; this.response = { success: false, result: null, errorMessage: error?.error?.detail || 'The operation request failed.', executionDurationMs: 0, executionId: 0, operationName: this.operation?.name || '', lastThreeExecutions: [], monthlyExecutionCount: 0, executionTrace: null }; });
  }

  get resultText(): string {
    if (!this.response) return '';
    return typeof this.response.result === 'string' ? this.response.result : JSON.stringify(this.response.result, null, 2);
  }
  inputType(config: InputConfig): string { return config.type?.toLowerCase() === 'integer' || config.type?.toLowerCase() === 'number' ? 'number' : config.type?.toLowerCase() === 'date' ? 'date' : 'text'; }
}
