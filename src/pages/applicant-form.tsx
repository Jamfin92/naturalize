import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Save } from 'lucide-react'
import { toast } from 'sonner'

import { PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { ApiError, api, type ApplicantInput } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import {
  APPLICATION_STATUSES,
  STATUS_LABELS,
  canManageApplicants,
  type ApplicationStatus,
  type Locality,
  type Lookup,
} from '@/lib/types'

/*
 * Add and edit an applicant. One component, two routes (/applicants/new and
 * /applicants/:id/edit), because the two forms differ only in what they are
 * seeded with and which verb they submit.
 */

/*
 * The form's own shape. It differs from ApplicantInput in one place: decisionDate
 * is a plain string here (an empty <input type=date> is ""), and is folded to
 * null on submit — the API's DateOnly? cannot parse "".
 */
type FormState = {
  alienNumber: string
  naturalizationNumber: string
  petitionNumber: string
  firstName: string
  middleName: string
  lastName: string
  birthDate: string
  admissionDate: string
  address1: string
  // Residence FK as a string (select value); folded to number|null on submit.
  localityId: string
  countryCode: string
  email: string
  status: ApplicationStatus
  decisionDate: string
  decisionNotes: string
}

const EMPTY: FormState = {
  alienNumber: '',
  naturalizationNumber: '',
  petitionNumber: '',
  firstName: '',
  middleName: '',
  lastName: '',
  birthDate: '',
  admissionDate: '',
  address1: '',
  localityId: '',
  countryCode: '',
  email: '',
  status: 'Received',
  decisionDate: '',
  decisionNotes: '',
}

/** Field-level errors as the API returns them (RFC7807 `errors`), keyed by camelCase field. */
type FieldErrors = Partial<Record<keyof FormState, string>>

export function ApplicantFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { officer } = useAuth()
  const editing = id !== undefined
  const allowed = canManageApplicants(officer)

  const [form, setForm] = useState<FormState>(EMPTY)
  const [errors, setErrors] = useState<FieldErrors>({})
  const [loading, setLoading] = useState(editing)
  const [saving, setSaving] = useState(false)
  const [localities, setLocalities] = useState<Locality[]>([])
  const [countries, setCountries] = useState<Lookup[]>([])

  useEffect(() => {
    let cancelled = false
    Promise.all([api.lookups.localities(), api.lookups.countries()])
      .then(([l, c]) => {
        if (cancelled) return
        setLocalities(l)
        setCountries(c)
      })
      .catch(() => {
        /* Non-fatal: the pickers just render empty. */
      })
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    if (!editing) return

    let cancelled = false
    api.applicants
      .get(Number(id))
      .then((a) => {
        if (cancelled) return
        setForm({
          alienNumber: a.alienNumber,
          naturalizationNumber: a.naturalizationNumber,
          petitionNumber: a.petitionNumber,
          firstName: a.firstName,
          middleName: a.middleName,
          lastName: a.lastName,
          birthDate: a.birthDate,
          admissionDate: a.admissionDate,
          address1: a.address1,
          localityId: a.localityId != null ? String(a.localityId) : '',
          countryCode: a.countryCode,
          email: a.email,
          status: a.status,
          decisionDate: a.decisionDate ?? '',
          decisionNotes: a.decisionNotes,
        })
      })
      .catch((e: unknown) => {
        if (!cancelled) toast.error(e instanceof Error ? e.message : 'Could not load applicant.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [id, editing])

  const set =
    (field: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      setForm((f) => ({ ...f, [field]: e.target.value }))
      setErrors((prev) => (prev[field] ? { ...prev, [field]: undefined } : prev))
    }

  const setValue = (field: keyof FormState) => (value: string) => {
    setForm((f) => ({ ...f, [field]: value }))
    setErrors((prev) => (prev[field] ? { ...prev, [field]: undefined } : prev))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})

    // Fold the empty decision date to null (the API's DateOnly? can't parse "")
    // and the locality select's string value to number|null for the FK.
    const body: ApplicantInput = {
      ...form,
      localityId: form.localityId ? Number(form.localityId) : null,
      decisionDate: form.decisionDate || null,
    }

    try {
      const saved = editing
        ? await api.applicants.update(Number(id), body)
        : await api.applicants.create(body)

      toast.success(editing ? 'Applicant updated.' : `${saved.fullName} added to the register.`)
      navigate(`/applicants/${saved.id}`)
    } catch (e: unknown) {
      if (e instanceof ApiError && e.status === 403) {
        toast.error('Your role does not permit changing applicant records.')
      } else if (e instanceof ApiError && e.status === 409) {
        // A 409 means this A-Number belongs to a WITHDRAWN applicant; surface the
        // server's message verbatim — it names the record and says what to do.
        setErrors({ alienNumber: e.message })
        toast.error('That A-Number is already on file.')
      } else if (e instanceof ApiError && Object.keys(e.fields).length > 0) {
        setErrors(
          Object.fromEntries(
            Object.entries(e.fields).map(([field, messages]) => [field, messages.join(' ')]),
          ) as FieldErrors,
        )
        toast.error('Please correct the highlighted fields.')
      } else {
        toast.error(e instanceof Error ? e.message : 'Could not save.')
      }
    } finally {
      setSaving(false)
    }
  }

  // A read-only Viewer can reach this route by typing the URL even though the
  // Edit / New buttons are hidden for them. Block it here too — the API would
  // return a 403 on save anyway.
  if (!allowed) {
    return (
      <div className="mx-auto max-w-3xl space-y-6">
        <PageHeader
          title="Not permitted"
          description="Your role can view applicant records but not change them."
          actions={
            <Button variant="ghost" onClick={() => navigate(-1)}>
              <ArrowLeft className="size-4" />
              Back
            </Button>
          }
        />
      </div>
    )
  }

  if (loading) {
    return <div className="text-muted-foreground p-2 text-sm">Loading…</div>
  }

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader
        title={editing ? 'Edit applicant' : 'New applicant'}
        description={
          editing
            ? 'Changes are recorded against this record’s history.'
            : 'Add a person to the naturalization register.'
        }
        actions={
          <Button variant="ghost" onClick={() => navigate(-1)}>
            <ArrowLeft className="size-4" />
            Back
          </Button>
        }
      />

      <form onSubmit={handleSubmit} noValidate>
        <Card>
          <CardHeader>
            <CardTitle className="font-heading text-base">Particulars</CardTitle>
          </CardHeader>

          <CardContent className="grid gap-5 @2xl:grid-cols-2">
            <div className="@2xl:col-span-2">
              <div className="grid gap-5 @xl:grid-cols-3">
                <Field
                  id="firstName"
                  label="First name"
                  value={form.firstName}
                  onChange={set('firstName')}
                  error={errors.firstName}
                  required
                />
                <Field
                  id="middleName"
                  label="Middle name"
                  value={form.middleName}
                  onChange={set('middleName')}
                  error={errors.middleName}
                />
                <Field
                  id="lastName"
                  label="Last name"
                  value={form.lastName}
                  onChange={set('lastName')}
                  error={errors.lastName}
                  required
                />
              </div>
            </div>

            <Field
              id="alienNumber"
              label="A-Number"
              value={form.alienNumber}
              onChange={set('alienNumber')}
              error={errors.alienNumber}
              placeholder="A123456789"
              required
            />
            <Field
              id="petitionNumber"
              label="Petition number"
              value={form.petitionNumber}
              onChange={set('petitionNumber')}
              error={errors.petitionNumber}
              placeholder="NBC2024123456"
            />
            <Field
              id="naturalizationNumber"
              label="Naturalization number"
              value={form.naturalizationNumber}
              onChange={set('naturalizationNumber')}
              error={errors.naturalizationNumber}
            />
            <SelectField
              id="status"
              label="Status"
              value={form.status}
              onValueChange={setValue('status')}
              error={errors.status}
              options={APPLICATION_STATUSES.map((s) => ({ value: s, label: STATUS_LABELS[s] }))}
            />

            <Field
              id="birthDate"
              label="Date of birth"
              type="date"
              value={form.birthDate}
              onChange={set('birthDate')}
              error={errors.birthDate}
              required
            />
            <Field
              id="admissionDate"
              label="Admission date"
              type="date"
              value={form.admissionDate}
              onChange={set('admissionDate')}
              error={errors.admissionDate}
              hint="Date admitted as an LPR — start of the continuous-residence clock (INA 316(a))."
              required
            />

            <div className="@2xl:col-span-2">
              <Field
                id="address1"
                label="Address"
                value={form.address1}
                onChange={set('address1')}
                error={errors.address1}
              />
            </div>

            <SelectField
              id="localityId"
              label="Residence"
              value={form.localityId}
              onValueChange={setValue('localityId')}
              error={errors.localityId}
              placeholder="Select a locality…"
              options={localities.map((l) => ({
                value: String(l.id),
                label: `${l.name}, ${l.state} ${l.zipCode}`,
              }))}
            />
            <SelectField
              id="countryCode"
              label="Country of birth"
              value={form.countryCode}
              onValueChange={setValue('countryCode')}
              error={errors.countryCode}
              placeholder="Select a country…"
              options={countries.map((c) => ({ value: c.code, label: c.description }))}
            />
            <Field
              id="email"
              label="Email"
              type="email"
              value={form.email}
              onChange={set('email')}
              error={errors.email}
            />

            <Field
              id="decisionDate"
              label="Decision date"
              type="date"
              value={form.decisionDate}
              onChange={set('decisionDate')}
              error={errors.decisionDate}
              hint="Leave blank until the application is decided."
            />
            <div />
            <div className="@2xl:col-span-2">
              <Field
                id="decisionNotes"
                label="Decision notes"
                value={form.decisionNotes}
                onChange={set('decisionNotes')}
                error={errors.decisionNotes}
              />
            </div>
          </CardContent>
        </Card>

        <div className="mt-6 flex items-center justify-end gap-3">
          <Button type="button" variant="ghost" onClick={() => navigate(-1)}>
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            <Save className="size-4" />
            {saving ? 'Saving…' : editing ? 'Save changes' : 'Add applicant'}
          </Button>
        </div>
      </form>
    </div>
  )
}

function Field({
  id,
  label,
  value,
  onChange,
  error,
  hint,
  type = 'text',
  placeholder,
  required,
}: {
  id: keyof FormState
  label: string
  value: string
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  error?: string
  hint?: string
  type?: string
  placeholder?: string
  required?: boolean
}) {
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined

  return (
    <div className="space-y-2">
      <Label htmlFor={id}>
        {label}
        {required && <span className="text-destructive ml-0.5">*</span>}
      </Label>
      <Input
        id={id}
        name={id}
        type={type}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={error ? 'border-destructive' : undefined}
      />
      {error ? (
        <p id={`${id}-error`} className="text-destructive text-xs">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-muted-foreground text-xs">
          {hint}
        </p>
      ) : null}
    </div>
  )
}

function SelectField({
  id,
  label,
  value,
  onValueChange,
  options,
  error,
  placeholder,
  required,
}: {
  id: keyof FormState
  label: string
  value: string
  onValueChange: (value: string) => void
  options: { value: string; label: string }[]
  error?: string
  placeholder?: string
  required?: boolean
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>
        {label}
        {required && <span className="text-destructive ml-0.5">*</span>}
      </Label>
      <Select value={value} onValueChange={onValueChange}>
        <SelectTrigger
          id={id}
          className={error ? 'border-destructive w-full' : 'w-full'}
          aria-invalid={error ? true : undefined}
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {options.map((o) => (
            <SelectItem key={o.value} value={o.value}>
              {o.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {error && (
        <p id={`${id}-error`} className="text-destructive text-xs">
          {error}
        </p>
      )}
    </div>
  )
}
