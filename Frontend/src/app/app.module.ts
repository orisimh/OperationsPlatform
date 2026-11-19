import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDividerModule } from '@angular/material/divider';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { AppComponent } from './app.component';
import { AppRoutingModule } from './app-routing.module';
import { DashboardComponent } from './dashboard/dashboard.component';
import { OperationsComponent } from './operations/operations.component';
import { OperationEditorComponent } from './operation-editor/operation-editor.component';
import { ExecuteComponent } from './execute/execute.component';
import { BuilderComponent } from './builder/builder.component';
import { HistoryComponent } from './history/history.component';
import { LogsComponent } from './logs/logs.component';

@NgModule({
  declarations: [AppComponent, DashboardComponent, OperationsComponent, OperationEditorComponent, ExecuteComponent, BuilderComponent, HistoryComponent, LogsComponent],
  imports: [
    BrowserModule, BrowserAnimationsModule, HttpClientModule, ReactiveFormsModule, FormsModule,
    DragDropModule, AppRoutingModule, MatSidenavModule, MatIconModule, MatButtonModule, MatToolbarModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatTableModule, MatTooltipModule,
    MatProgressSpinnerModule, MatSnackBarModule, MatChipsModule, MatCheckboxModule, MatDividerModule,
    MatMenuModule, MatPaginatorModule, MatSlideToggleModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {}
