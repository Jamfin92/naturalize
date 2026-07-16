import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Save } from 'lucide-react'
import { toast } from 'sonner'

import { PageHeader } from '@/components/page'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError, api, type ApplicantInput } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import { canManageApplicants } from '@/lib/types'

/*
 * Add and edit an applicant. One component, two routes (/applicants/new and
 * /applicants/:id/edit), because the two forms differ only in what they are
 * seeded with and which verb they submit — and a duplicated 12-field form is a
 * duplicated 12-field form to keep in sync forever.
 */

const EMPTY: ApplicantInput = {
  alienNumber: '',
  firstName: '',
  middleName: '',
  lastName: '',
  dateOfBirth: '',
  countryOfBirth: '',
  nationality: '',
  addressLine: '',
  city: '',
  state: '',
  postalCode: '',
  email: '',
  phone: '',
  lawfulPermanentResidentSince: '',
}

/** Field-level errors as the API returns them (RFC7807 `errors`), keyed by camelCase field. */
type FieldErrors = Partial<Record<keyof ApplicantInput, string>>

export function ApplicantFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { officer } = useAuth()
  const editing = id !== undefined
  const allowed = canManageApplicants(officer)

  const [form, setForm] = useState<ApplicantInput>(EMPTY)
  const [errors, setErrors] = useState<FieldErrors>({})
  const [loading, setLoading] = useState(editing)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!editing) return

    let cancelled = false
    api.applicants
      .get(Number(id))
      .then((a) => {
        if (cancelled) return
        // Drop the server-owned/computed fields; the rest of the shape is the form's.
        const { id: _id, createdAt: _createdAt, fullName: _fullName, ...rest } = a
        setForm(rest)
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

  const set = (field: keyof ApplicantInput) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setForm((f) => ({ ...f, [field]: e.target.value }))
    // Clear the error as soon as they start fixing it, rather than leaving it
    // red until they submit again.
    setErrors((prev) => (prev[field] ? { ...prev, [field]: undefined } : prev))
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})

    try {
      const saved = editing
        ? await api.applicants.update(Number(id), form)
        : await api.applicants.create(form)

      toast.success(editing ? 'Applicant updated.' : `${saved.fullName} added to the register.`)
      navigate(`/applicants/${saved.id}`)
    } catch (e: unknown) {
      /*
       * A 409 is the interesting one: it means this A-Number belongs to a
       * WITHDRAWN applicant, and the API is telling us to restore that record
       * rather than create a duplicate person. Surfacing the server's message
       * verbatim is right here — it names the record and says what to do.
       */
      if (e instanceof ApiError && e.status === 403) {
        toast.error('Your role does not permit changing applicant records.')
      } else if (e instanceof ApiError && e.status === 409) {
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
  // return a 403 on save anyway, so there is nothing to gain from the form.
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
              id="dateOfBirth"
              label="Date of birth"
              type="date"
              value={form.dateOfBirth}
              onChange={set('dateOfBirth')}
              error={errors.dateOfBirth}
              required
            />
            <Field
              id="lawfulPermanentResidentSince"
              label="LPR since"
              type="date"
              value={form.lawfulPermanentResidentSince}
              onChange={set('lawfulPermanentResidentSince')}
              error={errors.lawfulPermanentResidentSince}
              hint="Start of the continuous-residence clock (INA 316(a))."
              required
            />
            <Field
              id="countryOfBirth"
              label="Country of birth"
              value={form.countryOfBirth}
              onChange={set('countryOfBirth')}
              error={errors.countryOfBirth}
            />
            <Field
              id="nationality"
              label="Nationality"
              value={form.nationality}
              onChange={set('nationality')}
              error={errors.nationality}
            />

            <div className="@2xl:col-span-2">
              <Field
                id="addressLine"
                label="Address"
                value={form.addressLine}
                onChange={set('addressLine')}
                error={errors.addressLine}
              />
            </div>

            <Field id="city" label="City" value={form.city} onChange={set('city')} error={errors.city} />

            <div className="grid grid-cols-2 gap-4">
              <Field
                id="state"
                label="State"
                value={form.state}
                onChange={set('state')}
                error={errors.state}
              />
              <Field
                id="postalCode"
                label="ZIP"
                value={form.postalCode}
                onChange={set('postalCode')}
                error={errors.postalCode}
              />
            </div>

            <Field
              id="email"
              label="Email"
              type="email"
              value={form.email}
              onChange={set('email')}
              error={errors.email}
            />
            <Field
              id="phone"
              label="Phone"
              value={form.phone}
              onChange={set('phone')}
              error={errors.phone}
            />
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
  id: keyof ApplicantInput
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
