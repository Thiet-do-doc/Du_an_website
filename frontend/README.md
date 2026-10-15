# Frontend form for T1.2

Copy `AuthForms.tsx` into a React + TypeScript Vite frontend and render `<AuthForms />` from `App.tsx`.

Create `frontend/.env.local`:

```env
VITE_API_BASE_URL=https://localhost:7001
```

The API must allow the frontend origin and HTTPS certificate must be trusted. The form is a starter UI; before merging, adapt the API base URL, visual styles, routing, CSRF protection and existing shared components to the team's repository. The reset form accepts the token manually; you can improve it to parse `email` and `token` from the reset link query string.
