# Feature: Authentication

## Overview

Single-endpoint auth. Users log in with email + password and receive a JWT valid for 8 hours.
No registration flow — users are created by a SystemAdmin via `/api/admin/users`.

## Roles

| Role | Access |
|---|---|
| `Admin` | Own organization's apiaries, beehives, inspections, diets, todos |
| `SystemAdmin` | Everything + `/api/admin` (org and user management) |

## Login Flow

1. Client posts `{ email, password }` to `POST /api/auth/login`
2. `AuthService.LoginAsync` finds user by email, verifies BCrypt hash
3. On success: generates JWT with claims `userId`, `email`, `role`, `organizationId`
4. Client stores `token` and user object in localStorage keys `beehive_token` / `beehive_user`
5. `AuthContext` reads localStorage on mount, sets `user` state
6. Every Axios request attaches `Authorization: Bearer <token>`

## Business Rules

- Invalid email or wrong password → `BusinessRuleException` (mapped to 422, not 401 — avoids leaking which field is wrong)
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
