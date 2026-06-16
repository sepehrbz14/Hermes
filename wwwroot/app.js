const state = {
  holdings: [],
  watchlist: [],
  activity: [],
  companies: [],
  currencies: [],
  user: null,
  summary: null,
  activeScreen: "dashboard"
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => Array.from(document.querySelectorAll(selector));
const money = (value) => `IRR ${Number(value).toLocaleString("en-US", { maximumFractionDigits: 0 })}`;
const pct = (value) => `${Number(value) >= 0 ? "+" : ""}${Number(value).toFixed(2)}%`;

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options
  });

  if (response.status === 204) return null;

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(payload?.message || "The backend could not process that request.");
  }

  return payload;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function showToast(message) {
  const toast = $("#toast");
  toast.textContent = message;
  toast.classList.add("show");
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => toast.classList.remove("show"), 2400);
}

function switchAuth(target) {
  $("#loginCard").classList.toggle("hidden", target !== "login");
  $("#signupCard").classList.toggle("hidden", target !== "signup");
}

async function loadPortfolio() {
  const snapshot = await api("/api/portfolio");
  state.user = snapshot.user;
  state.holdings = snapshot.holdings || [];
  state.watchlist = snapshot.watchlist || [];
  state.activity = snapshot.activity || [];
  state.summary = snapshot.summary;

  if (state.user) {
    $("#signedInAs").textContent = state.user.name || state.user.email || "Investor";
    $("#displayName").value = state.user.name || "Investor";
  }

  render();
}

async function loadBrokerageStatus() {
  const status = await api("/api/brokerages/status");
  $("#brokerageMode").textContent = `${status.mode} mode`;
  $("#brokerageConnection").textContent = status.connected ? "Connected" : "Not connected";
  $("#brokerageMessage").textContent = status.message;
  $("#brokerageProviders").innerHTML = (status.pendingIntegrations || []).map((name) => `
    <div class="activity-item">
      <div class="activity-line"><strong>${escapeHtml(name)}</strong><span class="muted">${status.connected ? "Ready" : "Pending credentials"}</span></div>
    </div>
  `).join("");
}

async function loadCompanies(query = "") {
  state.companies = await api(`/api/market/companies?query=${encodeURIComponent(query)}&limit=50`);
  $("#companyOptions").innerHTML = state.companies.map((company) => `
    <option value="${escapeHtml(company.bourseSymbol)} - ${escapeHtml(company.fullTitle)}"></option>
  `).join("");
}

async function loadCurrencies() {
  state.currencies = await api("/api/market/currencies");
  $("#currencyPicker").innerHTML = state.currencies.map((currency) => `
    <option value="${currency.currencyId}">${escapeHtml(currency.currencyTitle)} (${escapeHtml(currency.currencySymbol)})</option>
  `).join("");
}

async function loadMarketLookups() {
  const results = await Promise.allSettled([loadCompanies(), loadCurrencies()]);
  const failed = results.find((result) => result.status === "rejected");
  if (failed) {
    showToast(failed.reason?.message || "Market lookup data could not be loaded right now.");
  }
}

async function enterApp(user) {
  state.user = user;
  $("#authShell").classList.add("hidden");
  $("#appShell").classList.remove("hidden");
  $("#signedInAs").textContent = user?.name || user?.email || "Investor";
  $("#displayName").value = user?.name || "Investor";
  await loadPortfolio();
  await loadBrokerageStatus();
  await loadMarketLookups();
}

async function logout() {
  await api("/api/auth/logout", { method: "POST" });
  state.user = null;
  $("#authShell").classList.remove("hidden");
  $("#appShell").classList.add("hidden");
  showToast("Signed out of Horizon.");
}

function getFilteredHoldings() {
  const query = $("#assetSearch").value.trim().toLowerCase();
  if (!query) return state.holdings;
  return state.holdings.filter((asset) =>
    asset.name.toLowerCase().includes(query) ||
    asset.ticker.toLowerCase().includes(query) ||
    asset.type.toLowerCase().includes(query)
  );
}

function holdingValue(asset) {
  return Number(asset.shares) * Number(asset.price);
}

function sparkline(change) {
  const up = Number(change) >= 0;
  const points = up ? "2,24 22,18 42,20 62,10 82,13 112,4" : "2,6 22,12 42,10 62,18 82,15 112,24";
  const color = up ? "#12ed9f" : "#ff5d70";
  return `<svg class="spark" viewBox="0 0 114 28" aria-hidden="true"><polyline points="${points}" fill="none" stroke="${color}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>`;
}

function renderTable(target, holdings) {
  target.innerHTML = holdings.map((asset) => `
    <tr>
      <td><div class="asset-cell"><span class="ticker">${escapeHtml(asset.ticker)}</span><span>${escapeHtml(asset.name)}</span></div></td>
      <td>${escapeHtml(asset.type)}</td>
      <td>${Number(asset.shares).toLocaleString("en-US")}</td>
      <td>${money(asset.price)}</td>
      <td>${money(holdingValue(asset))}</td>
      <td class="${Number(asset.change) >= 0 ? "gain" : "loss"}">${pct(asset.change)}</td>
      <td>${sparkline(asset.change)}</td>
      <td><button class="icon-btn btn" type="button" aria-label="Remove ${escapeHtml(asset.name)}" data-remove-id="${asset.id}">x</button></td>
    </tr>
  `).join("");
}

function renderSummary() {
  const summary = state.summary || {};
  $("#totalValue").textContent = money(summary.totalValue || 0);
  $("#dailyMove").textContent = money(summary.dailyMove || 0);
  $("#dailyPct").textContent = pct(summary.dailyPercent || 0);
  $("#dailyPct").className = Number(summary.dailyPercent || 0) >= 0 ? "gain" : "loss";
  $("#totalGain").textContent = pct(summary.totalGainPercent || 0);
  $("#holdingsCount").textContent = `${state.holdings.length} assets`;
  $("#riskScore").textContent = summary.riskScore || 0;
}

function renderAllocation() {
  const totals = state.holdings.reduce((map, asset) => {
    map[asset.type] = (map[asset.type] || 0) + holdingValue(asset);
    return map;
  }, {});
  const colors = ["#12ed9f", "#14cbe6", "#f2c45a", "#8d7cff", "#ff7a90"];
  const entries = Object.entries(totals).sort((a, b) => b[1] - a[1]);
  const total = entries.reduce((sum, item) => sum + item[1], 0) || 1;
  let cursor = 0;
  const gradient = entries.map(([type, value], index) => {
    const start = cursor;
    const amount = value / total * 100;
    cursor += amount;
    return `${colors[index % colors.length]} ${start}% ${cursor}%`;
  }).join(", ");
  $("#donutChart").style.background = `conic-gradient(${gradient || "#12ed9f 0 100%"})`;
  $("#allocationLegend").innerHTML = entries.map(([type, value], index) => `
    <div class="legend-row"><span class="dot" style="background:${colors[index % colors.length]}"></span><span>${escapeHtml(type)}</span><strong>${(value / total * 100).toFixed(1)}%</strong></div>
  `).join("");
}

function renderWatchlist() {
  $("#watchlist").innerHTML = state.watchlist.map((item) => `
    <div class="watch-item">
      <div class="watch-line"><strong>${escapeHtml(item.ticker)}</strong><span>${money(item.price)}</span></div>
      <div class="watch-line muted"><span>${escapeHtml(item.name)}</span><span class="${Number(item.change) >= 0 ? "gain" : "loss"}">${pct(item.change)}</span></div>
    </div>
  `).join("");
}

function renderActivity() {
  $("#activityFeed").innerHTML = state.activity.slice(0, 8).map((item, index) => `
    <div class="activity-item">
      <div class="activity-line"><strong>${escapeHtml(item)}</strong><span class="muted">${index === 0 ? "Now" : `${index + 1}h ago`}</span></div>
      <span class="muted">Horizon recorded this update through the ASP.NET backend.</span>
    </div>
  `).join("");
}

function render() {
  const filtered = getFilteredHoldings();
  renderTable($("#holdingsBody"), filtered);
  renderTable($("#holdingsBodyFull"), filtered);
  renderSummary();
  renderAllocation();
  renderWatchlist();
  renderActivity();
}

function setScreen(screen) {
  state.activeScreen = screen;
  $$(".screen").forEach((el) => el.classList.toggle("active", el.id === `${screen}Screen`));
  $$(".nav button").forEach((btn) => btn.classList.toggle("active", btn.dataset.screen === screen));
  const titles = {
    dashboard: ["Portfolio Overview", "A focused command center for your Horizon portfolio."],
    holdings: ["Holdings", "Search, review, add, or remove Tehran market positions."],
    activity: ["Activity", "Recent portfolio events and rebalance prompts."],
    settings: ["Settings", "Personalize your Horizon workspace and brokerage connections."]
  };
  $("#screenTitle").textContent = titles[screen][0];
  $("#screenSubtitle").textContent = titles[screen][1];
  $("#assetSearch").classList.toggle("hidden", screen === "settings" || screen === "activity");
}

function passwordScore(value) {
  let score = 0;
  if (value.length >= 8) score += 25;
  if (/[A-Z]/.test(value)) score += 25;
  if (/[0-9]/.test(value)) score += 25;
  if (/[^A-Za-z0-9]/.test(value)) score += 25;
  return score;
}

function setBusy(button, busy) {
  button.disabled = busy;
  button.classList.toggle("disabled", busy);
}

$$("[data-auth-switch]").forEach((btn) => btn.addEventListener("click", () => switchAuth(btn.dataset.authSwitch)));
$("#forgotBtn").addEventListener("click", () => showToast("Password reset placeholder reached on the frontend."));
$("#logoutBtn").addEventListener("click", () => logout().catch((error) => showToast(error.message)));
$("#assetSearch").addEventListener("input", render);
$("#refreshWatch").addEventListener("click", async () => {
  await loadPortfolio();
  showToast("Watchlist refreshed from the backend.");
});

$("#brokerageSync").addEventListener("click", async (event) => {
  setBusy(event.currentTarget, true);
  try {
    const result = await api("/api/brokerages/sync", { method: "POST" });
    await loadPortfolio();
    showToast(result.message);
  } catch (error) {
    showToast(error.message);
  } finally {
    setBusy(event.currentTarget, false);
  }
});

let companySearchTimer;
$("#marketCompanySearch").addEventListener("input", (event) => {
  clearTimeout(companySearchTimer);
  const value = event.target.value.trim();
  companySearchTimer = setTimeout(async () => {
    try {
      await loadCompanies(value);
      const selected = state.companies.find((company) => `${company.bourseSymbol} - ${company.fullTitle}` === value || company.bourseSymbol === value);
      if (!selected) return;

      $("#assetCompanyId").value = selected.coId;
      $("#assetName").value = selected.fullTitle;
      $("#assetTicker").value = selected.bourseSymbol;
      $("#assetType").value = selected.isFund ? "ETF" : "Stock";

      const quote = await api(`/api/market/companies/${selected.coId}/quote`);
      $("#assetPrice").value = quote.closingPrice || quote.lastPrice || "";
      showToast(`${selected.bourseSymbol} loaded from NADPCO.`);
    } catch (error) {
      showToast(error.message);
    }
  }, 250);
});

$("#loadCurrencyValue").addEventListener("click", async (event) => {
  setBusy(event.currentTarget, true);
  try {
    const currencyId = Number($("#currencyPicker").value);
    const values = await api("/api/market/currencies/values", {
      method: "POST",
      body: JSON.stringify({ currencyIds: [currencyId] })
    });
    const selected = values[0];
    $("#currencyCloseValue").value = selected?.currencyCloseValue == null ? "-" : money(selected.currencyCloseValue);
    $("#currencyUpdatedAt").value = selected?.updatedAt ? new Date(selected.updatedAt).toLocaleString() : "-";
    showToast("Currency close value loaded.");
  } catch (error) {
    showToast(error.message);
  } finally {
    setBusy(event.currentTarget, false);
  }
});

$("#signupPassword").addEventListener("input", (event) => {
  const score = passwordScore(event.target.value);
  $("#strengthFill").style.width = `${Math.max(8, score)}%`;
  $("#strengthFill").style.background = score > 70 ? "#25e75c" : score > 35 ? "#f2c45a" : "#ff5d70";
  $("#strengthLabel").textContent = score > 70 ? "Password Strength: Strong" : score > 35 ? "Password Strength: Medium" : "Password Strength: Weak";
});

$("#googleLogin").addEventListener("click", async () => {
  const user = await api("/api/auth/google", { method: "POST" });
  await enterApp(user);
});

$("#googleSignup").addEventListener("click", async () => {
  const user = await api("/api/auth/google", { method: "POST" });
  await enterApp(user);
});

$("#loginForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const submit = event.submitter;
  setBusy(submit, true);
  try {
    const user = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({
        email: $("#loginEmail").value.trim(),
        password: $("#loginPassword").value,
        rememberMe: $("#rememberMe").checked
      })
    });
    $("#loginError").textContent = "";
    await enterApp(user);
  } catch (error) {
    $("#loginError").textContent = error.message;
  } finally {
    setBusy(submit, false);
  }
});

$("#signupForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const submit = event.submitter;
  if (!$("#terms").checked) {
    $("#signupError").textContent = "Accept the terms to create a Horizon account.";
    return;
  }
  if (passwordScore($("#signupPassword").value) < 35) {
    $("#signupError").textContent = "Use a stronger password before creating an account.";
    return;
  }

  setBusy(submit, true);
  try {
    const user = await api("/api/auth/signup", {
      method: "POST",
      body: JSON.stringify({
        name: $("#fullName").value.trim(),
        email: $("#signupEmail").value.trim(),
        phone: $("#phone").value.trim(),
        password: $("#signupPassword").value,
        confirmPassword: $("#confirmPassword").value
      })
    });
    $("#signupError").textContent = "";
    await enterApp(user);
  } catch (error) {
    $("#signupError").textContent = error.message;
  } finally {
    setBusy(submit, false);
  }
});

$$(".nav button").forEach((btn) => btn.addEventListener("click", () => setScreen(btn.dataset.screen)));
$("#addAssetBtn").addEventListener("click", () => $("#assetModal").classList.remove("hidden"));
$("#cancelAsset").addEventListener("click", () => $("#assetModal").classList.add("hidden"));
$("#assetModal").addEventListener("click", (event) => {
  if (event.target.id === "assetModal") $("#assetModal").classList.add("hidden");
});

$("#assetForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const submit = event.submitter;
  setBusy(submit, true);
  try {
    const asset = {
      name: $("#assetName").value.trim(),
      ticker: $("#assetTicker").value.trim(),
      type: $("#assetType").value,
      shares: Number($("#assetShares").value),
      price: Number($("#assetPrice").value),
      companyId: $("#assetCompanyId").value ? Number($("#assetCompanyId").value) : null
    };
    await api("/api/portfolio/holdings", { method: "POST", body: JSON.stringify(asset) });
    event.target.reset();
    $("#assetModal").classList.add("hidden");
    await loadPortfolio();
    showToast(`${asset.ticker} added to Horizon.`);
  } catch (error) {
    showToast(error.message);
  } finally {
    setBusy(submit, false);
  }
});

document.addEventListener("click", async (event) => {
  const removeButton = event.target.closest("[data-remove-id]");
  if (!removeButton) return;

  const asset = state.holdings.find((item) => item.id === removeButton.dataset.removeId);
  setBusy(removeButton, true);
  try {
    await api(`/api/portfolio/holdings/${removeButton.dataset.removeId}`, { method: "DELETE" });
    await loadPortfolio();
    showToast(`${asset?.ticker || "Holding"} removed.`);
  } catch (error) {
    showToast(error.message);
  } finally {
    setBusy(removeButton, false);
  }
});

$("#displayName").addEventListener("change", async (event) => {
  try {
    const user = await api("/api/portfolio/display-name", {
      method: "POST",
      body: JSON.stringify({ name: event.target.value || "Investor" })
    });
    state.user = user;
    $("#signedInAs").textContent = user.name;
    await loadPortfolio();
  } catch (error) {
    showToast(error.message);
  }
});

loadPortfolio()
  .then(loadBrokerageStatus)
  .then(loadMarketLookups)
  .then(() => {
    if (state.user) {
      $("#authShell").classList.add("hidden");
      $("#appShell").classList.remove("hidden");
    }
  })
  .catch((error) => showToast(error.message));
