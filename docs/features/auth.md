# Feature: Authentication

> ⚠️ Parts of this file below the **Login Flow** section predate self-registration, refresh-token
> rotation and the four-role model — `../context.md` is the accurate summary. Only the identifier
> sections have been kept current.

## Overview

Users log in with **an email address or a phone number**, plus a password.

## Login Identifiers

Either identifier signs the same account in. `LoginDto.Identifier` carries whatever the user typed
and `AuthService.FindByIdentifierAsync` routes on `'@'`: no email address lacks one and no phone
number contains one.

- **Phone numbers are stored canonically** (E.164, e.g. `+38761123456`) via
  `Common/Validation/PhoneRules.Normalize`. `061 123 456`, `+387 61 123 456`, `0038761123456` and
  `61123456` are one value — without that, `User.Phone`'s unique index would let the same person
  register twice, and signing up in one notation then signing in with another would fail.
  Bare numbers are assumed BiH (`+387`); an explicit `+` or `00` prefix is left alone.
- **A number is required wherever an account is created** — self-registration,
  `/api/admin/users`, and org member creation. `User.Phone` is still **nullable**: accounts that
  predate the field have none and keep signing in by email, and only non-null values are covered
  by the unique index.
- **It can be changed** from the profile (self-service) and from admin user edit. Two rules make
  that safe, both locked by `PhoneUniquenessTests`:
  - **Blank means "leave it alone", never "clear it".** A client cached before this change omits
    the field entirely, and JSON gives no way to tell that apart from an explicit blank — treating
    it as a clear would silently strip a login identifier off every account an admin edited.
  - **Uniqueness excludes your own account** (`IsPhoneTakenAsync(phone, excludeUserId)`). Without
    that, saving any other profile field would fail for everyone who already has a number.
    The value is normalised *before* being compared to the stored one, so re-submitting the same
    number written differently is not even treated as a change.
- **The phone is not verified.** There is no SMS anywhere in the system, so an entered number is an
  unproven claim. That is acceptable for *identifying* an account (the password is still the proof)
  but means it must never become a **recovery** channel: password reset stays email-only. Adding
  "reset by SMS" without verifying the number first would be an account-takeover path.
- `LoginDto` still accepts `email` as an alias for `identifier`, because the frontend is an
  installable PWA and a client cached before the rename would otherwise be locked out.

## Roles

| Role | Access |
|---|---|
| `Admin` | Own organization's apiaries, beehives, inspections, diets, todos |
| `SystemAdmin` | Everything + `/api/admin` (org and user management) |

## Login Flow

1. Client posts `{ identifier, password }` to `POST /api/auth/login`
2. `AuthService.LoginAsync` resolves the identifier to a user (see above), verifies BCrypt hash.
   A BCrypt verify always runs, even with no match, so "no such account" and "wrong password"
   cost the same and return the same message
3. On success: generates JWT with claims `userId`, `email`, `role`, `organizationId`
4. Client stores `token` and user object in localStorage keys `beehive_token` / `beehive_user`
5. `AuthContext` reads localStorage on mount, sets `user` state
6. Every Axios request attaches `Authorization: Bearer <token>`

## Business Rules

- Unknown identifier or wrong password → `UnauthorizedException` (401) with one shared message
- Registration rejects a duplicate email *and* a duplicate phone → 422, message names the field
- All endpoints except `POST /api/auth/login` require a valid token
- A 401 response on any request triggers automatic logout and redirect to `/login`
- JWT is stateless — no server-side session, no refresh token currently

## Frontend Routes

- `/login` — public, redirects to `/apiaries` (or `/admin`) if already authenticated
- `/` — `SmartRedirect` redirects based on `user.role`
- All other routes wrapped in `ProtectedRoute`
- `/admin/*` wrapped in `AdminRoute` (requires `SystemAdmin`)

## Edge Cases

- Expired token: backend returns 401 → frontend logs out automatically
- User deleted while logged in: next API call returns 401 → auto logout
- SystemAdmin accessing org-scoped data: `organizationId` claim still applies; Admin controller has no org filter

## Security Notes

- Passwords hashed with BCrypt (work factor default ~12)
- JWT secret minimum 32 characters, stored in `appsettings.json` — must be in env var for production
- `appsettings.Production.json` is in `.gitignore`
