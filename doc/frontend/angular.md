# Angular Frontend

The frontend is located in `frontend/` and uses Angular 20. The application has lazy routes, an authentication guard, a JWT interceptor, and services for Auth, Sales, Users, and Catalog.

## Integration

- Development uses `proxy.conf.json` to forward `/api` to the local API.
- Compose uses Nginx to forward `/api` to the WebApi service.
- The JWT is stored in `localStorage` and attached by the interceptor.

Using `localStorage` is simple for local evaluation but increases the impact of XSS. For production, evaluate HttpOnly/SameSite cookies, CSP, and other session controls.

## Verification

```powershell
npm ci
npm test -- --no-progress
npm run lint
npm run build
```

Current tests mainly cover services, the interceptor, and validators. Components, the guard, and complete Sales operations still have coverage gaps.
