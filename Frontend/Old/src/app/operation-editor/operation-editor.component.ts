import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { OperationDefinition, UpsertOperationRequest } from '../models/operation.models';
import { OperationsApiService } from '../services/operations-api.service';

const DEFAULT_CSHARP = `using System;\nusing System.Text.Json;\n\npublic static class CustomOperation\n{\n    public static object? Execute(string? fieldA, string? fieldB, JsonElement configuration)\n    {\n        // Return a JSON-serializable result.\n        return $"{fieldA}{fieldB}";\n    }\n}`;

@Component({ selector: 'app-operation-editor', templateUrl: './operation-editor.component.html', styleUrls: ['./operation-editor.component.scss'] })
export class OperationEditorComponent implements OnInit {
  form: FormGroup;
  isEdit = false;
  operationId: number | null = null;
  loading = false;
  validating = false;
  sourceCode = DEFAULT_CSHARP;
  operationConfigurationJson = '{}';
  diagnostics: string[] = [];
  configError = '';

  constructor(private fb: FormBuilder, private route: ActivatedRoute, private router: Router, private api: OperationsApiService, private snack: MatSnackBar) {
    this.form = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(120), Validators.pattern(/^[a-zA-Z0-9._-]+$/)]],
      name: ['', [Validators.required, Validators.maxLength(160)]],
      description: [''], operationType: ['String', Validators.required],
      implementationType: ['BuiltIn', Validators.required], outputType: ['String', Validators.required],
      category: ['General', Validators.required], icon: ['bolt'], sortOrder: [200], isActive: [true],
      inputAConfigJson: ['{"type":"String","label":"Field A","required":true}', Validators.required],
      inputBConfigJson: ['{"type":"String","label":"Field B","required":true}', Validators.required],
      configurationJson: ['{"handlerKey":"string.concatenate"}', Validators.required],
      restUrl: ['https://api.example.com/lookup'], restMethod: ['GET'],
      restHeadersJson: ['{\n  "Accept": "application/json"\n}'],
      restQueryParametersJson: ['{\n  "first": "{{fieldA}}",\n  "second": "{{fieldB}}"\n}'],
      restBodyTemplate: [''], restContentType: ['application/json'], restTimeoutSeconds: [15],
      restResponseType: ['Json'], restAuthType: ['None'], restAuthHeaderName: ['X-API-Key'], restSecretValue: ['']
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) { this.isEdit = true; this.operationId = Number(id); this.load(this.operationId); }
  }

  load(id: number): void {
    this.loading = true;
    this.api.getOperation(id).subscribe(item => {
      const config = JSON.parse(item.configurationJson || '{}');
      if (item.implementationType === 'CSharp') {
        this.sourceCode = config.sourceCode || DEFAULT_CSHARP;
        this.operationConfigurationJson = JSON.stringify(config.operationConfiguration || {}, null, 2);
      }
      if (item.implementationType === 'RestApi') {
        this.form.patchValue({
          restUrl: config.url || '', restMethod: config.httpMethod || 'GET',
          restHeadersJson: JSON.stringify(config.headers || {}, null, 2),
          restQueryParametersJson: JSON.stringify(config.queryParameters || {}, null, 2),
          restBodyTemplate: config.bodyTemplate || '', restContentType: config.contentType || 'application/json',
          restTimeoutSeconds: config.timeoutSeconds || 15, restResponseType: config.responseType || 'Json',
          restAuthType: config.authentication?.type || 'None',
          restAuthHeaderName: config.authentication?.headerName || 'X-API-Key',
          restSecretValue: config.authentication?.secretValue && config.authentication.secretValue !== '***REDACTED***' ? config.authentication.secretValue : ''
        });
      }
      this.form.patchValue({ ...item, configurationJson: item.implementationType === 'CSharp' || item.implementationType === 'RestApi' ? '{}' : JSON.stringify(config, null, 2) });
      this.loading = false;
    }, () => { this.loading = false; this.snack.open('Could not load operation.', 'Close', { duration: 3500 }); this.router.navigate(['/operations']); });
  }

  onImplementationChange(value: string): void {
    if (value === 'CSharp' && !this.sourceCode) this.sourceCode = DEFAULT_CSHARP;
    if (value === 'BuiltIn' && this.form.get('configurationJson')?.value === '{}') this.form.patchValue({ configurationJson: '{\n  "handlerKey": "string.concatenate",\n  "separator": ""\n}' });
    if (value === 'RestApi') this.form.patchValue({
      operationType: 'RestApi', outputType: 'Json', category: 'External Services',
      restUrl: 'https://api.example.com/lookup', restMethod: 'GET',
      inputAConfigJson: '{\"type\":\"String\",\"label\":\"Field A\",\"required\":true}',
      inputBConfigJson: '{\"type\":\"String\",\"label\":\"Field B\",\"required\":true}'
    });
  }

  validateCode(): void {
    this.validating = true; this.diagnostics = [];
    this.api.validateCSharp(this.sourceCode).subscribe(result => {
      this.validating = false; this.diagnostics = result.diagnostics || [];
      this.snack.open(result.success ? 'C# compilation succeeded.' : (result.message || 'Compilation failed.'), 'Close', { duration: 4500 });
    }, error => { this.validating = false; this.snack.open(error?.error?.detail || 'C# validation failed. Is the worker built?', 'Close', { duration: 5000 }); });
  }

  save(): void {
    this.configError = '';
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    let configuration: any;
    try {
      JSON.parse(this.form.value.inputAConfigJson);
      JSON.parse(this.form.value.inputBConfigJson);
      if (this.form.value.implementationType === 'CSharp') {
        configuration = { sourceCode: this.sourceCode, operationConfiguration: JSON.parse(this.operationConfigurationJson || '{}') };
      } else if (this.form.value.implementationType === 'RestApi') {
        const authentication: any = { type: this.form.value.restAuthType };
        if (this.form.value.restAuthType === 'Bearer' && this.form.value.restSecretValue) authentication.secretValue = this.form.value.restSecretValue;
        if (this.form.value.restAuthType === 'ApiKeyHeader') { authentication.headerName = this.form.value.restAuthHeaderName; if (this.form.value.restSecretValue) authentication.secretValue = this.form.value.restSecretValue; }
        configuration = {
          url: this.form.value.restUrl, httpMethod: this.form.value.restMethod,
          headers: JSON.parse(this.form.value.restHeadersJson || '{}'),
          queryParameters: JSON.parse(this.form.value.restQueryParametersJson || '{}'),
          bodyTemplate: this.form.value.restBodyTemplate, contentType: this.form.value.restContentType,
          timeoutSeconds: Number(this.form.value.restTimeoutSeconds), responseType: this.form.value.restResponseType,
          authentication
        };
      } else {
        configuration = JSON.parse(this.form.value.configurationJson);
      }
    } catch (e) { this.configError = 'One of the JSON configuration fields is invalid. Check the JSON syntax.'; return; }

    const request: UpsertOperationRequest = {
      ...this.form.value,
      executionMode: 'Server',
      configurationJson: JSON.stringify(configuration)
    };
    this.loading = true;
    const call = this.isEdit && this.operationId ? this.api.updateOperation(this.operationId, request) : this.api.createOperation(request);
    call.subscribe(() => {
      this.loading = false;
      this.snack.open(this.isEdit ? 'Operation updated.' : 'Operation created.', 'Close', { duration: 3000 });
      this.router.navigate(['/operations']);
    }, error => {
      this.loading = false;
      this.configError = error?.error?.detail || error?.error?.title || 'Could not save operation. Check the fields and try again.';
    });
  }
}
