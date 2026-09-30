import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { HrApiService } from '../hr-api.service';
import { DepartmentDto, EmployeeDto, EmployeeInput, EmployeeStatus } from '../hr.models';

export interface EmployeeFormDialogData {
  employee: EmployeeDto | null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-employee-form-dialog',
  imports: [TranslatePipe, ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  styleUrl: './employee-form-dialog.scss',
  templateUrl: './employee-form-dialog.html'
})
export class EmployeeFormDialog {
  private readonly fb = inject(FormBuilder);
  private readonly hrApi = inject(HrApiService);
  private readonly dialogRef = inject(MatDialogRef<EmployeeFormDialog, EmployeeInput>);
  readonly data = inject<EmployeeFormDialogData>(MAT_DIALOG_DATA);

  readonly isEdit = this.data.employee !== null;
  readonly departments = signal<DepartmentDto[]>([]);
  readonly statuses: EmployeeStatus[] = ['Active', 'OnLeave', 'Terminated'];

  readonly form = this.fb.nonNullable.group({
    fullName: [this.data.employee?.fullName ?? '', [Validators.required, Validators.maxLength(200)]],
    email: [this.data.employee?.email ?? '', [Validators.required, Validators.email, Validators.maxLength(320)]],
    jobTitle: [this.data.employee?.jobTitle ?? '', [Validators.maxLength(200)]],
    departmentId: this.fb.control<number | null>(this.data.employee?.departmentId ?? null),
    hireDate: [this.data.employee?.hireDate?.slice(0, 10) ?? ''],
    status: this.fb.nonNullable.control<EmployeeStatus>(this.data.employee?.status ?? 'Active')
  });

  constructor() {
    this.hrApi.listAllDepartments().subscribe((departments) => this.departments.set(departments));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.dialogRef.close({
      fullName: value.fullName,
      email: value.email,
      jobTitle: value.jobTitle || null,
      departmentId: value.departmentId,
      hireDate: value.hireDate || null,
      status: value.status
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
