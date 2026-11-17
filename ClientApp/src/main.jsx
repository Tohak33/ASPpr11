import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { apiBase, blockedOrigin, callApi, frontendOrigin, restoreSession, saveSession } from './api';
import './styles.css';

const blocked = window.location.origin !== frontendOrigin;
const emptyCredentials = { login: '', password: '', fullName: '' };
const statusNames = {
  200: 'OK', 201: 'Created', 400: 'Bad Request', 401: 'Unauthorized',
  403: 'Forbidden', 404: 'Not Found', 409: 'Conflict', 429: 'Too Many Requests',
};

function Outcome({ result }) {
  if (!result) return <p className="empty-result" role="status">Здесь появится результат запроса.</p>;
  let message = result.note;
  if (result.networkError) {
    message = blocked
      ? 'Браузер не дал прочитать ответ. Этот адрес не входит в список CORS. Если API запущен, открой разрешённый адрес и повтори запрос.'
      : 'Браузер не получил ответ. Проверь, что API запущен и адрес клиента разрешён настройками CORS.';
  } else if (!message) {
    message = {
      400: 'Проверь данные запроса.',
      401: 'Вход не выполнен или токен больше не действителен.',
      403: 'Вход выполнен, но прав для этого действия нет.',
      404: 'Запись не найдена.',
      409: 'Запрос конфликтует с текущими данными.',
      429: 'Превышен лимит попыток входа.',
    }[result.status] || (result.ok ? 'Запрос выполнен.' : 'API вернул ошибку.');
    if (result.data?.errors) message = Object.values(result.data.errors).flat()[0] ?? message;
    else if (result.data?.title && result.data.title !== 'Unauthorized' && result.data.title !== 'Forbidden') {
      message = result.data.title;
    }
    if (result.retryAfter) message += ' Повтори через ' + result.retryAfter + ' с.';
  }
  return (
    <div className={'outcome ' + (result.ok ? 'success' : 'failure')} role="status">
      <div className="result-top">
        <strong>{result.networkError ? (blocked ? 'CORS / нет ответа' : 'Нет ответа') : result.status + ' ' + (statusNames[result.status] || '')}</strong>
        <code>{result.method} {result.path}</code>
      </div>
      <p>{message}</p>
    </div>
  );
}

function App() {
  const [session, setSession] = useState(restoreSession);
  const [mode, setMode] = useState('login');
  const [credentials, setCredentials] = useState(emptyCredentials);
  const [target, setTarget] = useState('');
  const [fullName, setFullName] = useState('');
  const [result, setResult] = useState(null);
  const [profile, setProfile] = useState(null);
  const [busy, setBusy] = useState(false);

  function updateSession(next) {
    saveSession(next);
    setSession(next);
  }

  async function perform(action) {
    setBusy(true);
    setResult(null);
    setProfile(null);
    try { await action(); } finally { setBusy(false); }
  }

  async function getProfile(path, accessToken) {
    const res = await callApi('GET', path, accessToken);
    setProfile(res.ok ? res.data : null);
    setResult({ ...res, note: res.ok ? 'API принял токен и вернул профиль.' : null });
    return res;
  }

  function authenticate(event) {
    event.preventDefault();
    perform(async () => {
      const body = { login: credentials.login, password: credentials.password };
      if (mode === 'register') body.fullName = credentials.fullName;
      const res = await callApi('POST', '/api/auth/' + mode, null, body);
      setResult(res);
      if (!res.ok) return;
      updateSession(res.data);
      setCredentials(emptyCredentials);
      setTarget(String(res.data.user.id));
      setFullName(res.data.user.fullName);
      await getProfile('/api/profiles/me', res.data.accessToken);
    });
  }

  function signOut() {
    updateSession(null);
    setCredentials(emptyCredentials);
    setProfile(null);
    setResult(null);
    setTarget('');
    setFullName('');
    setMode('login');
  }

  function checkOwnProfile() {
    perform(async () => {
      const res = await getProfile('/api/profiles/me', session.accessToken);
      if (res.ok) {
        setTarget(String(res.data.id));
        setFullName(res.data.fullName);
      }
    });
  }

  function checkAccess(path, token, note) {
    perform(async () => {
      const res = await callApi('GET', path, token);
      setResult({ ...res, note: res.ok ? note : null });
    });
  }

  function checkTarget(event) {
    event.preventDefault();
    perform(async () => {
      const res = await getProfile('/api/profiles/' + target, session.accessToken);
      if (res.ok) setFullName(res.data.fullName);
    });
  }

  function editProfile() {
    if (!target || !fullName.trim()) return;
    perform(async () => {
      const res = await callApi('PATCH', '/api/profiles/' + target, session.accessToken, { fullName });
      setProfile(res.ok ? res.data : null);
      if (res.ok && res.data.id === session.user.id) updateSession({ ...session, user: res.data });
      setResult({ ...res, note: res.ok ? 'Имя профиля сохранено.' : null });
    });
  }

  return (
    <main className="wrap">
      <header className="header">
        <span className="brand">para<span>/</span>api</span>
        <span className="marker">Практическая 11</span>
      </header>
      <section className="intro">
        <p className="eyebrow">РЕЖИМ ПРОВЕРКИ</p>
        <h1>Кто может получить доступ</h1>
        <p>Войди через API, проверь права на свой и чужой профиль и посмотри, как браузер применяет CORS.</p>
      </section>

      <div className="layout">
        <section className="panel" aria-label={session ? 'Доступ к профилям' : 'Вход и регистрация'}>
          {!session ? (
            <>
              <div className="tabs" aria-label="Способ входа">
                <button type="button" aria-pressed={mode === 'login'} disabled={busy} onClick={() => { setMode('login'); setResult(null); }}>Вход</button>
                <button type="button" aria-pressed={mode === 'register'} disabled={busy} onClick={() => { setMode('register'); setResult(null); }}>Регистрация</button>
              </div>
              <h2>{mode === 'login' ? 'Войти в магазин' : 'Создать аккаунт'}</h2>
              <form onSubmit={authenticate}>
                <label>Логин
                  <input required minLength={mode === 'register' ? 3 : 1} maxLength={50} autoComplete="username" value={credentials.login}
                    onChange={e => setCredentials({ ...credentials, login: e.target.value })} />
                </label>
                {mode === 'register' && <label>Имя
                  <input required maxLength={100} autoComplete="name" value={credentials.fullName}
                    onChange={e => setCredentials({ ...credentials, fullName: e.target.value })} />
                </label>}
                <label>Пароль
                  <input type="password" required minLength={mode === 'register' ? 6 : 1} maxLength={100}
                    autoComplete={mode === 'login' ? 'current-password' : 'new-password'} value={credentials.password}
                    onChange={e => setCredentials({ ...credentials, password: e.target.value })} />
                </label>
                <button className="primary" disabled={busy} type="submit">{busy ? 'Подождите…' : mode === 'login' ? 'Войти' : 'Зарегистрироваться'}</button>
              </form>
              <p className="hint">Регистрация выдаёт роль User. Можно войти и под уже существующим аккаунтом магазина.</p>
            </>
          ) : (
            <>
              <div className="session-top">
                <div><p className="eyebrow">АКТИВНАЯ СЕССИЯ</p><h2>{session.user.fullName}</h2>
                  <p className="hint">{session.user.login} · ID {session.user.id} · <strong>{session.user.role}</strong></p></div>
                <button type="button" disabled={busy} onClick={signOut}>Выйти</button>
              </div>
              <div className="actions">
                <button type="button" disabled={busy} onClick={checkOwnProfile}>Мой профиль</button>
                <button type="button" disabled={busy} onClick={() => checkAccess('/api/users?page=1&pageSize=1', session.accessToken, 'Доступ к списку пользователей разрешён роли Admin.')}>Метод для Admin</button>
              </div>
              <form className="target-form" onSubmit={checkTarget}>
                <label>ID профиля
                  <input type="number" min={1} max={2147483647} step={1} required value={target} onChange={e => setTarget(e.target.value)} />
                </label>
                <label>Новое имя для PATCH
                  <input maxLength={100} value={fullName} onChange={e => setFullName(e.target.value)} />
                </label>
                <div className="actions">
                  <button type="submit" disabled={busy}>Загрузить по ID</button>
                  <button className="primary" type="button" disabled={busy || !/^[1-9]\d*$/.test(target) || Number(target) > 2147483647 || !fullName.trim()} onClick={editProfile}>Сохранить имя</button>
                </div>
              </form>
              <p className="hint">User может открыть и изменить свой профиль. Admin может работать с любым профилем.</p>
            </>
          )}
        </section>

        <section className="panel results" aria-label="Проверка CORS и ответы">
          <p className="eyebrow">КЛИЕНТ → API</p>
          <h2>Результат запроса</h2>
          <dl className="addresses">
            <div><dt>Клиент</dt><dd><code>{window.location.origin}</code></dd></div>
            <div><dt>API</dt><dd><code>{apiBase}</code></dd></div>
          </dl>
          <Outcome result={result} />
          {profile && (
            <dl className="profile" aria-label="Данные профиля">
              <div><dt>ID</dt><dd>{profile.id}</dd></div>
              <div><dt>Логин</dt><dd>{profile.login}</dd></div>
              <div><dt>Имя</dt><dd>{profile.fullName}</dd></div>
              <div><dt>Роль</dt><dd>{profile.role}</dd></div>
            </dl>
          )}
          <div className="actions probes">
            <button type="button" disabled={busy} onClick={() => checkAccess('/api/profiles/me', null)}>Запрос без JWT</button>
            <button type="button" disabled={busy} onClick={() => checkAccess('/api/categories', null, 'Браузер получил ответ API с другого origin.')}>Проверить CORS</button>
          </div>
          <p className="hint">Для сравнения открой этот же клиент по другому адресу и снова нажми «Проверить CORS».</p>
          {blocked ? <a href={frontendOrigin}>Открыть разрешённый адрес</a> : <a href={blockedOrigin}>Открыть адрес вне CORS</a>}
        </section>
      </div>
    </main>
  );
}

createRoot(document.getElementById('root')).render(<App />);
