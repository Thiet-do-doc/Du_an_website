import { FormEvent, useState } from "react";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ""; // Uses Vite /api proxy in local development.
type Mode = "login" | "register" | "forgot" | "reset";

async function callApi(path: string, body: unknown) {
  const response = await fetch(`${API_BASE}/api/v1/auth/${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(body),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const errors = Array.isArray(data.errors)
      ? data.errors.map((e: { description?: string }) => e.description).join("; ")
      : "";
    throw new Error(data.message ?? errors ?? "Có lỗi xảy ra. Vui lòng thử lại.");
  }
  return data;
}

export default function AuthForms() {
  const [mode, setMode] = useState<Mode>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [dateOfBirth, setDateOfBirth] = useState("");
  const [token, setToken] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(""); setError(""); setBusy(true);
    try {
      if (mode === "register") {
        const result = await callApi("register", { email, password, dateOfBirth });
        setMessage(result.message ?? "Đăng ký thành công.");
      } else if (mode === "login") {
        const result = await callApi("login", { email, password });
        setMessage(result.message ?? "Đăng nhập thành công.");
      } else if (mode === "forgot") {
        const result = await callApi("forgot-password", { email });
        setMessage(result.message ?? "Nếu email hợp lệ, hướng dẫn sẽ được gửi.");
      } else {
        const result = await callApi("reset-password", { email, token, newPassword });
        setMessage(result.message ?? "Đặt lại mật khẩu thành công.");
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Không thể kết nối máy chủ.");
    } finally { setBusy(false); }
  }

  return (
    <main className="auth-page">
      <section className="auth-card">
        <p className="auth-eyebrow">DATING APP</p>
        <h1>{mode === "register" ? "Tạo tài khoản" : mode === "forgot" ? "Quên mật khẩu" : mode === "reset" ? "Đặt lại mật khẩu" : "Chào mừng trở lại"}</h1>
        <p className="auth-subtitle">{mode === "register" ? "Bạn cần đủ 18 tuổi và xác nhận email." : "Đăng nhập an toàn để tiếp tục kết nối."}</p>
        <form onSubmit={submit}>
          <label>Email
            <input type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} required maxLength={256} />
          </label>
          {mode === "register" && <label>Ngày sinh
            <input type="date" value={dateOfBirth} onChange={e => setDateOfBirth(e.target.value)} required max={new Date().toISOString().slice(0, 10)} />
          </label>}
          {mode === "login" || mode === "register" ? <label>Mật khẩu
            <input type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} value={password} onChange={e => setPassword(e.target.value)} required minLength={mode === "register" ? 10 : 1} />
          </label> : null}
          {mode === "reset" && <>
            <label>Token đặt lại mật khẩu
              <input value={token} onChange={e => setToken(e.target.value)} required />
            </label>
            <label>Mật khẩu mới
              <input type="password" autoComplete="new-password" value={newPassword} onChange={e => setNewPassword(e.target.value)} required minLength={10} />
            </label>
          </>}
          {error && <p className="auth-error" role="alert">{error}</p>}
          {message && <p className="auth-success" role="status">{message}</p>}
          <button type="submit" disabled={busy}>{busy ? "Đang xử lý..." : mode === "register" ? "Đăng ký" : mode === "forgot" ? "Gửi hướng dẫn" : mode === "reset" ? "Đổi mật khẩu" : "Đăng nhập"}</button>
        </form>
        <nav className="auth-links" aria-label="Tùy chọn tài khoản">
          {mode !== "login" && <button type="button" onClick={() => { setMode("login"); setError(""); setMessage(""); }}>Đăng nhập</button>}
          {mode !== "register" && <button type="button" onClick={() => { setMode("register"); setError(""); setMessage(""); }}>Tạo tài khoản</button>}
          {mode !== "forgot" && <button type="button" onClick={() => { setMode("forgot"); setError(""); setMessage(""); }}>Quên mật khẩu?</button>}
          {mode !== "reset" && <button type="button" onClick={() => { setMode("reset"); setError(""); setMessage(""); }}>Đã có token đặt lại</button>}
        </nav>
      </section>
      <style>{`
        .auth-page{min-height:100vh;display:grid;place-items:center;padding:24px;background:linear-gradient(135deg,#fff1f2,#f5f3ff);font-family:Inter,system-ui,sans-serif;color:#1f2937}
        .auth-card{width:min(100%,430px);padding:32px;border-radius:24px;background:white;box-shadow:0 20px 60px #312e8118}
        .auth-eyebrow{font-size:12px;font-weight:800;letter-spacing:.18em;color:#be123c}
        h1{font-size:28px;margin:8px 0}.auth-subtitle{color:#6b7280;margin:0 0 24px;line-height:1.5}
        form{display:grid;gap:16px}label{display:grid;gap:7px;font-size:14px;font-weight:600}
        input{box-sizing:border-box;width:100%;padding:12px 13px;border:1px solid #d1d5db;border-radius:10px;font:inherit}input:focus{outline:2px solid #fda4af;border-color:#e11d48}
        form>button{border:0;border-radius:10px;padding:13px;background:#e11d48;color:white;font-weight:700;cursor:pointer}form>button:disabled{opacity:.65}
        .auth-error,.auth-success{font-size:14px;line-height:1.45;margin:0}.auth-error{color:#b91c1c}.auth-success{color:#047857}
        .auth-links{display:flex;flex-wrap:wrap;gap:8px 14px;margin-top:20px}.auth-links button{border:0;background:transparent;padding:0;color:#be123c;cursor:pointer;font-size:13px}
      `}</style>
    </main>
  );
}
