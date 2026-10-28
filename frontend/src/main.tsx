import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import 'bootstrap/dist/css/bootstrap.min.css';
import './app/styles.css';
import AppRouter from './app/AppRouter';
import { AuthProvider } from './features/auth/AuthProvider';
import './app/mylove.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode><AuthProvider><AppRouter /></AuthProvider></StrictMode>,
);
