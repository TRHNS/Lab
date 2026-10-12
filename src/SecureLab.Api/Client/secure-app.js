import { apiFetch } from "./api-fetch.js";

const state = document.querySelector("#session-state");
const profile = document.querySelector("#profile");
const profileResult = document.querySelector("#profile-result");
const registerResult = document.querySelector("#register-result");
const loginResult = document.querySelector("#login-result");
const incidentResult = document.querySelector("#incident-result");

function postJson(url, data) {
    return apiFetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data)
    });
}

// Показуємо профіль поточного користувача
async function loadProfile() {
    profile.replaceChildren();
    const response = await apiFetch("/api/me");
    const isJson = response.headers.get("Content-Type")?.includes("application/json");

    if (response.ok && isJson) {
        const me = await response.json();
        for (const [name, value] of [["Id", me.id], ["Ім’я", me.userName], ["Роль", me.role]]) {
            const dt = document.createElement("dt");
            const dd = document.createElement("dd");
            dt.textContent = name;
            dd.textContent = value;
            profile.append(dt, dd);
        }
        state.textContent = "Сеанс активний";
        profileResult.textContent = "";
    } else if (response.status === 401) {
        state.textContent = "Не ввійшли";
        profileResult.textContent = "Профіль недоступний (401)";
    } else {
        profileResult.textContent = `Помилка: ${response.status}`;
    }
}

// Реєстрація
document.querySelector("#register-form").addEventListener("submit", async e => {
    e.preventDefault();
    const form = e.currentTarget;
    const data = Object.fromEntries(new FormData(form));
    const response = await postJson("/api/auth/register", data);
    form.elements.password.value = "";
    registerResult.textContent = response.status === 201
        ? "Реєстрація успішна (201), тепер увійдіть"
        : `Реєстрація: ${response.status}`;
});

// Вхід
document.querySelector("#login-form").addEventListener("submit", async e => {
    e.preventDefault();
    const form = e.currentTarget;
    const data = Object.fromEntries(new FormData(form));
    const response = await postJson("/api/auth/login", data);
    form.elements.password.value = "";
    loginResult.textContent = `Login: ${response.status}`;
    if (response.status === 204) await loadProfile();
});

// Вихід
document.querySelector("#logout-button").addEventListener("click", async () => {
    const response = await apiFetch("/api/auth/logout", { method: "POST" });
    loginResult.textContent = `Logout: ${response.status}`;
    incidentResult.textContent = "";
    await loadProfile(); 
});

// Створення інциденту 
document.querySelector("#incident-form").addEventListener("submit", async e => {
    e.preventDefault();
    const form = e.currentTarget;
    const data = new FormData(form);
    const response = await postJson("/api/incidents", {
        title: data.get("title"),
        description: data.get("description"),
        severity: data.get("severity"),
        occurredAtUtc: new Date(data.get("occurredAt")).toISOString()
    });
    if (response.status === 201) {
        const created = await response.json();
        incidentResult.textContent = `Створено (201): ${created.title}`;
        form.reset();
    } else {
        incidentResult.textContent = `Помилка: ${response.status}`;
    }
});

loadProfile();