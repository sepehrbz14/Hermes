const state = {
  holdings: [],
  watchlist: [],
  activity: [],
  companies: [],
  currencies: [],
  instruments: [],
  marketResults: [],
  user: null,
  summary: null,
  activeScreen: "dashboard"
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => Array.from(document.querySelectorAll(selector));
const money = (value) => `IRR ${Number(value).toLocaleString("en-US", { maximumFractionDigits: 0 })}`;
const pct = (value) => `${Number(value) >= 0 ? "+" : ""}${Number(value).toFixed(2)}%`;
const displayPrice = (value) => value == null ? t("marketClose") : money(value);
const displayChange = (value) => value == null ? t("marketClose") : pct(value);

const translations = {
  en: {
    language: "Language", loginTitle: "Log In", signupTitle: "Create Account", remember: "Remember me", forgot: "Forgot Password?", loginSubmit: "Log In to Account", signupSubmit: "Create My Account", orLogin: "Or log in with", orSignup: "Or sign up with", noAccount: "Don't have an account?", haveAccount: "Already have an account?", signUp: "Sign Up", logIn: "Log in", overview: "Overview", holdings: "Holdings", settings: "Settings", cashReady: "Cash ready", signedInAs: "Signed in as", searchHoldings: "Search holdings", addHolding: "Add Holding", logout: "Log out", totalValue: "Total Value", dailyMove: "Daily Move", dividends: "Dividends", riskScore: "Risk Score", recentHoldings: "Recent Holdings", allocation: "Allocation", liveMix: "Live mix", watchlist: "Watchlist", marketSearch: "Market Search", searchMarket: "Search Market", searchPlaceholder: "Search symbol, company, ETF, or currency", stocksEtfsCurrencies: "Stocks, ETFs, currencies", instrument: "Instrument", type: "Type", price: "Price", today: "Today", allHoldings: "All Holdings", editablePortfolio: "Editable portfolio", preferences: "Preferences", backendSynced: "Backend synced", displayName: "Display Name", currency: "Currency", riskStyle: "Risk Style", marketData: "NADPCO Market Data", checkFeed: "Check Feed", searchEmpty: "Search for a stock, ETF, or currency.", watch: "Watch", add: "Add", marketClose: "Market Close", enterManual: "Market Close - enter price manually", dashboardTitle: "Portfolio Overview", dashboardSubtitle: "A focused command center for your Horizon portfolio.", holdingsTitle: "Holdings", holdingsSubtitle: "Search, review, add, or remove Tehran market positions.", settingsTitle: "Settings", settingsSubtitle: "Personalize your Horizon workspace and brokerage connections.", addedHolding: "added to Horizon.", chooseInstrument: "Choose an instrument before saving.", enterPrice: "Enter a valid price before saving.", enterAmount: "Enter a valid amount before saving."
  },
  fa: {
    language: "زبان", loginTitle: "ورود", signupTitle: "ایجاد حساب", remember: "مرا به خاطر بسپار", forgot: "رمز عبور را فراموش کرده‌اید؟", loginSubmit: "ورود به حساب", signupSubmit: "ایجاد حساب من", orLogin: "یا ورود با", orSignup: "یا ثبت‌نام با", noAccount: "حساب کاربری ندارید؟", haveAccount: "قبلاً حساب ساخته‌اید؟", signUp: "ثبت‌نام", logIn: "ورود", overview: "نمای کلی", holdings: "دارایی‌ها", settings: "تنظیمات", cashReady: "وجه نقد آماده", signedInAs: "واردشده با نام", searchHoldings: "جست‌وجوی دارایی‌ها", addHolding: "افزودن دارایی", logout: "خروج", totalValue: "ارزش کل", dailyMove: "تغییر روزانه", dividends: "سود نقدی", riskScore: "امتیاز ریسک", recentHoldings: "دارایی‌های اخیر", allocation: "ترکیب دارایی", liveMix: "ترکیب زنده", watchlist: "فهرست پیگیری", marketSearch: "جست‌وجوی بازار", searchMarket: "جست‌وجوی بازار", searchPlaceholder: "جست‌وجوی نماد، شرکت، صندوق یا ارز", stocksEtfsCurrencies: "سهام، صندوق‌ها و ارزها", instrument: "ابزار", type: "نوع", price: "قیمت", today: "امروز", allHoldings: "همه دارایی‌ها", editablePortfolio: "سبد قابل ویرایش", preferences: "ترجیحات", backendSynced: "همگام با سرور", displayName: "نام نمایشی", currency: "واحد پول", riskStyle: "سبک ریسک", marketData: "داده بازار نادپکو", checkFeed: "بررسی داده", searchEmpty: "برای یافتن سهام، صندوق یا ارز جست‌وجو کنید.", watch: "پیگیری", add: "افزودن", marketClose: "بازار بسته است", enterManual: "بازار بسته است؛ قیمت را دستی وارد کنید", dashboardTitle: "نمای کلی سبد", dashboardSubtitle: "مرکز فرماندهی متمرکز برای مدیریت سبد هورایزن شما.", holdingsTitle: "دارایی‌ها", holdingsSubtitle: "جست‌وجو، بررسی، افزودن یا حذف موقعیت‌های بازار تهران.", settingsTitle: "تنظیمات", settingsSubtitle: "شخصی‌سازی فضای کاری و اتصال‌های بازار هورایزن.", addedHolding: "به هورایزن اضافه شد.", chooseInstrument: "قبل از ذخیره، یک ابزار انتخاب کنید.", enterPrice: "قبل از ذخیره، قیمت معتبر وارد کنید.", enterAmount: "قبل از ذخیره، مقدار معتبر وارد کنید."
  }
};


Object.assign(translations.en, {
  emailUser: "Email / Username", password: "Password", fullName: "Full Name", emailAddress: "Email Address", phoneNumber: "Phone Number", confirmPassword: "Confirm Password", amount: "Amount", priceRial: "Price in Rial", purchaseDate: "Purchase Date", assetName: "Asset Name", symbol: "Symbol", saveHolding: "Save Holding", cancel: "Cancel", searchInstrument: "Search Instrument", shares: "Shares", value: "Value", trend: "Trend"
});

Object.assign(translations.fa, {
  emailUser: "ایمیل یا نام کاربری", password: "رمز عبور", fullName: "نام و نام خانوادگی", emailAddress: "نشانی ایمیل", phoneNumber: "شماره تلفن", confirmPassword: "تکرار رمز عبور", amount: "مقدار", priceRial: "قیمت به ریال", purchaseDate: "تاریخ خرید", assetName: "نام دارایی", symbol: "نماد", saveHolding: "ذخیره دارایی", cancel: "انصراف", searchInstrument: "جست‌وجوی ابزار", shares: "تعداد", value: "ارزش", trend: "روند"
});

let currentLanguage = localStorage.getItem("language") || "en";
const t = (key) => translations[currentLanguage]?.[key] || translations.en[key] || key;

function setText(selector, key) {
  const element = $(selector);
  if (element) element.textContent = t(key);
}

function setPlaceholder(selector, key) {
  const element = $(selector);
  if (element) element.placeholder = t(key);
}

function setInputLabel(selector, key) {
  const element = $(selector);
  const label = element?.closest("label") || element;
  if (!label) return;
  const textNode = Array.from(label.childNodes).find((node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim());
  if (textNode) textNode.textContent = t(key);
}

function applyLanguage(language = currentLanguage) {
  currentLanguage = language;
  localStorage.setItem("language", language);
  document.documentElement.lang = language === "fa" ? "fa" : "en";
  document.body.classList.toggle("rtl", language === "fa");
  const languageSelect = $("#languageSelect");
  if (languageSelect) languageSelect.value = language;

  setText(".language-switcher label", "language");
  setText("#loginCard h2", "loginTitle");
  setText("#signupCard h2", "signupTitle");
  setInputLabel("#rememberMe", "remember");
  setText("#forgotBtn", "forgot");
  setText("#loginForm .primary-btn", "loginSubmit");
  setText("#signupForm .primary-btn", "signupSubmit");
  setText(".nav [data-screen='dashboard']", "overview");
  setText(".nav [data-screen='holdings']", "holdings");
  setText(".nav [data-screen='settings']", "settings");
  setText("#addAssetBtn", "addHolding");
  setText("#logoutBtn", "logout");
  setText("#assetModalTitle", "addHolding");
  setText("#brokerageSync", "checkFeed");
  setText(".sidebar-card:nth-of-type(1) span", "cashReady");
  setText(".sidebar-card:nth-of-type(2) span", "signedInAs");
  setText("#dashboardScreen .metric:nth-child(1) span", "totalValue");
  setText("#dashboardScreen .metric:nth-child(2) span", "dailyMove");
  setText("#dashboardScreen .metric:nth-child(3) span", "dividends");
  setText("#dashboardScreen .metric:nth-child(4) span", "riskScore");
  setText("#dashboardScreen .panel:nth-of-type(1) h3", "recentHoldings");
  setText("#dashboardScreen .panel:nth-of-type(2) h3", "allocation");
  setText("#dashboardScreen .panel:nth-of-type(3) h3", "watchlist");
  setText("#holdingsScreen .panel:nth-of-type(1) h3", "marketSearch");
  setText("#holdingsScreen .panel:nth-of-type(1) .section-label", "stocksEtfsCurrencies");
  setText("#holdingsScreen .panel:nth-of-type(2) h3", "allHoldings");
  setText("#holdingsScreen .panel:nth-of-type(2) .section-label", "editablePortfolio");
  setText("#settingsScreen .panel:nth-of-type(1) h3", "preferences");
  setText("#settingsScreen .panel:nth-of-type(1) .section-label", "backendSynced");
  setText("#settingsScreen .panel:nth-of-type(2) h3", "marketData");
  setPlaceholder("#assetSearch", "searchHoldings");
  setPlaceholder("#marketSearch", "searchPlaceholder");
  setPlaceholder("#marketCompanySearch", "searchPlaceholder");
  setPlaceholder("#assetPrice", "enterManual");
  setPlaceholder("#loginEmail", "emailUser");
  setPlaceholder("#loginPassword", "password");
  setPlaceholder("#fullName", "fullName");
  setPlaceholder("#signupEmail", "emailAddress");
  setPlaceholder("#phone", "phoneNumber");
  setPlaceholder("#signupPassword", "password");
  setPlaceholder("#confirmPassword", "confirmPassword");
  setInputLabel("#marketCompanySearch", "searchInstrument");
  setInputLabel("#assetName", "assetName");
  setInputLabel("#assetTicker", "symbol");
  setInputLabel("#assetType", "type");
  setInputLabel("#assetShares", "amount");
  setInputLabel("#assetPrice", "priceRial");
  setInputLabel("#assetDate", "purchaseDate");
  setText("#cancelAsset", "cancel");
  setText("#assetForm .small-primary", "saveHolding");
  $$("#holdingsScreen th").forEach((th, index) => {
    const keys = ["instrument", "type", "price", "today", "", "", "instrument", "type", "shares", "price", "value", "today", "trend", ""];
    if (keys[index]) th.textContent = t(keys[index]);
  });
  setScreen(state.activeScreen || "dashboard");
  render();
}

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

const staticLookupCache = {
  companies: null,
  currencies: null
};

async function loadStaticLookup(path, cacheKey, regexFallback) {
  if (staticLookupCache[cacheKey]) return staticLookupCache[cacheKey];

  const response = await fetch(path, { cache: "force-cache" });
  if (!response.ok) {
    throw new Error(`${path} could not be loaded.`);
  }

  const text = await response.text();
  try {
    const parsed = JSON.parse(text);
    staticLookupCache[cacheKey] = Array.isArray(parsed) ? parsed.filter((item) => item && typeof item === "object") : [];
  } catch {
    staticLookupCache[cacheKey] = regexFallback(text);
  }

  return staticLookupCache[cacheKey];
}

function parseStaticCompanies(text) {
  while (text.includes('\\"')) text = text.replaceAll('\\"', '"');
  text = text.replaceAll('\\n', '\n');
  const companies = [];
  const pattern = /\"coID\"\s*:\s*(\d+)[\s\S]*?\"coTitle\"\s*:\s*\"([^\"]*)\"[\s\S]*?\"coSymbol\"\s*:\s*\"([^\"]*)\"[\s\S]*?\"marketTitle\"\s*:\s*\"([^\"]*)\"[\s\S]*?\"precedencyRight\"\s*:\s*(\d+)[\s\S]*?\"fundTypeID\"\s*:\s*(null|\d+)[\s\S]*?\"fundTypeTitle\"\s*:\s*(null|\"([^\"]*)\")/g;
  let match;
  while ((match = pattern.exec(text)) !== null) {
    companies.push({
      coID: Number(match[1]),
      coTitle: match[2],
      coSymbol: match[3],
      marketTitle: match[4],
      precedencyRight: Number(match[5]),
      fundTypeID: match[6] === "null" ? null : Number(match[6]),
      fundTypeTitle: match[8] || null
    });
  }
  return companies;
}

function parseStaticCurrencies(text) {
  while (text.includes('\\"')) text = text.replaceAll('\\"', '"');
  text = text.replaceAll('\\n', '\n');
  const currencies = [];
  const pattern = /"currencyId"\s*:\s*(\d+)[\s\S]*?"currencySymbol"\s*:\s*"([^"]*)"[\s\S]*?"currencyTitle"\s*:\s*"([^"]*)"/g;
  let match;
  while ((match = pattern.exec(text)) !== null) {
    currencies.push({
      currencyId: Number(match[1]),
      currencySymbol: match[2],
      currencyTitle: match[3]
    });
  }
  return currencies;
}

const matchesLookupQuery = (query, ...values) => {
  const normalizedQuery = (query || "").trim().toLocaleLowerCase();
  return !normalizedQuery || values.some((value) => String(value || "").toLocaleLowerCase().includes(normalizedQuery));
};

async function loadCompanies(query = "") {
  const companies = await loadStaticLookup("/Companies.json", "companies", parseStaticCompanies);
  state.companies = companies
    .filter((company) => Number(company.precedencyRight ?? company.PrecedencyRight ?? 0) === 0)
    .map((company) => ({
      coId: Number(company.coID ?? company.coId ?? company.CoID),
      bourseSymbol: company.coSymbol ?? company.bourseSymbol ?? company.CoSymbol ?? company.BourseSymbol ?? "",
      fullTitle: company.coTitle ?? company.fullTitle ?? company.CoTitle ?? company.FullTitle ?? "",
      symbolEnglish: company.coSymbolEnglish ?? company.symbolEnglish ?? company.CoSymbolEnglish,
      marketTitle: company.marketTitle ?? company.MarketTitle,
      industryTitle: company.industryTitle ?? company.IndustryTitle,
      isFund: Boolean(company.fundTypeID ?? company.FundTypeID) || String(company.fundTypeTitle ?? company.FundTypeTitle ?? "").includes("صندوق")
    }))
    .filter((company) => company.coId && company.bourseSymbol && company.fullTitle)
    .filter((company) => matchesLookupQuery(query, company.bourseSymbol, company.fullTitle, company.symbolEnglish))
    .sort((first, second) => Number(second.isFund) - Number(first.isFund) || first.bourseSymbol.localeCompare(second.bourseSymbol))
    .slice(0, 50);
}

async function loadInstruments(query = "") {
  await loadCompanies(query);
  const currencies = (await loadStaticLookup("/Currencies.json", "currencies", parseStaticCurrencies))
    .filter((currency) => matchesLookupQuery(query, currency.currencySymbol, currency.currencyTitle))
    .slice(0, 50);

  state.instruments = state.companies.map((company) => ({
    source: "company",
    sourceId: company.coId,
    symbol: company.bourseSymbol,
    title: company.fullTitle,
    type: company.isFund ? "ETF" : "Tehran Stock",
    price: null,
    change: null,
    subtitle: company.marketTitle || company.industryTitle
  })).concat(currencies.map((currency) => ({
    source: "currency",
    sourceId: currency.currencyId,
    symbol: currency.currencySymbol,
    title: currency.currencyTitle,
    type: "Currency",
    price: null,
    change: 0,
    subtitle: "Priced in Rial"
  }))).slice(0, 50);

  $("#companyOptions").innerHTML = state.instruments.map((instrument) => `
    <option value="${escapeHtml(instrument.symbol)} - ${escapeHtml(instrument.title)}" label="${escapeHtml(instrument.type)}"></option>
  `).join("");
}

async function loadCurrencies() {
  state.currencies = await loadStaticLookup("/Currencies.json", "currencies", parseStaticCurrencies);
  const currencyPicker = $("#currencyPicker");
  if (!currencyPicker) return;

  currencyPicker.innerHTML = state.currencies.map((currency) => `
    <option value="${currency.currencyId}">${escapeHtml(currency.currencyTitle)} (${escapeHtml(currency.currencySymbol)})</option>
  `).join("");
}

async function loadMarketLookups() {
  const results = await Promise.allSettled([loadInstruments(), loadCurrencies()]);
  const failed = results.find((result) => result.status === "rejected");
  if (failed) {
    showToast(failed.reason?.message || "Market lookup data could not be loaded right now.");
  }
}

async function loadInstrumentQuote(instrument) {
  const quote = await api(`/api/market/instruments/${instrument.source}/${instrument.sourceId}/quote`);
  return {
    ...instrument,
    symbol: quote.symbol === "Market Close" ? instrument.symbol : quote.symbol || quote.bourseSymbol || instrument.symbol,
    title: quote.title === "Market Close" ? instrument.title : quote.title || quote.fullTitle || instrument.title,
    price: quote.price ?? quote.closingPrice ?? quote.lastPrice ?? instrument.price ?? null,
    change: quote.change ?? quote.closingPChgPercent ?? instrument.change ?? null,
    subtitle: quote.subtitle || quote.tradeDate || instrument.subtitle || (quote.price == null && quote.closingPrice == null && quote.lastPrice == null ? "Market Close" : null)
  };
}

async function refreshWatchlistQuotes(button) {
  setBusy(button, true);
  try {
    const refreshed = await Promise.all(state.watchlist.map(async (item) => {
      const instrument = {
        source: item.source,
        sourceId: item.sourceId,
        symbol: item.ticker,
        title: item.name,
        type: item.type,
        price: item.price,
        change: item.change
      };
      const quote = await loadInstrumentQuote(instrument).catch(() => ({ ...instrument, subtitle: "Market Close" }));

      return {
        ...item,
        ticker: quote.symbol,
        name: quote.title,
        price: quote.price,
        change: quote.change,
        marketStatus: quote.subtitle === "Market Close" ? "Market Close" : null
      };
    }));

    state.watchlist = refreshed;
    renderWatchlist();
    showToast("Watchlist refreshed with live NADPCO quotes.");
  } catch (error) {
    showToast(error.message);
  } finally {
    setBusy(button, false);
  }
}

function showFirstLoadingError(results, fallbackMessage) {
  const failed = results.find((result) => result.status === "rejected");
  if (failed) {
    showToast(failed.reason?.message || fallbackMessage);
  }
}

async function loadAppData() {
  const results = await Promise.allSettled([
    loadPortfolio(),
    loadBrokerageStatus(),
    loadMarketLookups()
  ]);
  showFirstLoadingError(results, "Some dashboard data could not be loaded right now.");
}

async function enterApp(user) {
  state.user = user;
  $("#authShell").classList.add("hidden");
  $("#appShell").classList.remove("hidden");
  $("#signedInAs").textContent = user?.name || user?.email || "Investor";
  $("#displayName").value = user?.name || "Investor";
  setScreen("dashboard");
  render();
  loadAppData();
}

async function enterPortfolioOverviewFromPlaceholder() {
  const placeholderUser = {
    name: "Google Investor",
    email: "google@hermes.local",
    authProvider: "google-placeholder"
  };
  await enterApp(placeholderUser);
  showToast("Google sign-in placeholder opened the portfolio overview.");
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
  if (!holdings.length) {
    target.innerHTML = `<tr><td colspan="8"><div class="empty-state"><div><p>No holdings to display yet.</p><span class="muted">Add your first holding to see it here.</span><br><button class="small-primary" type="button" data-open-holding-modal>+ ${t("addHolding")}</button></div></div></td></tr>`;
    return;
  }

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
  if (!state.watchlist.length) {
    $("#watchlist").innerHTML = `<div class="empty-state"><div><p>No watchlist instruments yet.</p><span class="muted">Search the market and add instruments you want to follow.</span></div></div>`;
    return;
  }

  $("#watchlist").innerHTML = state.watchlist.map((item) => `
    <div class="watch-item">
      <div class="watch-line"><strong>${escapeHtml(item.ticker)}</strong><span>${displayPrice(item.price)}</span></div>
      <div class="watch-line muted"><span>${escapeHtml(item.name)}</span><span class="${item.change == null ? "muted" : Number(item.change) >= 0 ? "gain" : "loss"}">${item.marketStatus || displayChange(item.change)}</span></div>
      <div class="watch-line"><span class="muted">${escapeHtml(item.type)}</span><button class="icon-btn watch-remove" type="button" aria-label="Remove ${escapeHtml(item.name)} from watchlist" data-remove-watch-source="${escapeHtml(item.source)}" data-remove-watch-id="${item.sourceId}">×</button></div>
    </div>
  `).join("");
}

function fillAssetFormFromInstrument(instrument) {
  $("#assetSource").value = instrument.source;
  $("#assetCompanyId").value = instrument.source === "company" ? instrument.sourceId : "";
  $("#assetCurrencyId").value = instrument.source === "currency" ? instrument.sourceId : "";
  $("#assetName").value = instrument.title;
  $("#assetTicker").value = instrument.symbol;
  $("#assetType").value = instrument.type === "Tehran Stock" ? "Stock" : instrument.type;
  $("#assetPrice").value = instrument.price ?? "";
  $("#assetPrice").placeholder = instrument.price == null ? t("enterManual") : "";
  if (!$("#assetDate").value) {
    $("#assetDate").valueAsDate = new Date();
  }
}

function openHoldingModal(instrument = null) {
  if (!instrument) {
    $("#assetForm").reset();
  }

  if (instrument) {
    fillAssetFormFromInstrument(instrument);
  } else if (!$("#assetDate").value) {
    $("#assetDate").valueAsDate = new Date();
  }
  $("#assetModal").classList.remove("hidden");
}

function renderMarketResults() {
  const target = $("#marketResults");
  if (!target) return;

  target.innerHTML = state.marketResults.map((instrument) => `
    <tr>
      <td><div class="asset-cell"><span class="ticker">${escapeHtml(instrument.symbol)}</span><span>${escapeHtml(instrument.title)}</span></div></td>
      <td>${escapeHtml(instrument.type)}</td>
      <td>${displayPrice(instrument.price)}</td>
      <td class="${instrument.change == null ? "muted" : Number(instrument.change) >= 0 ? "gain" : "loss"}">${instrument.subtitle === "Market Close" ? t("marketClose") : displayChange(instrument.change)}</td>
      <td><button class="small-primary" type="button" data-add-holding-source="${escapeHtml(instrument.source)}" data-add-holding-id="${instrument.sourceId}">${t("add")}</button></td>
      <td><button class="small-primary" type="button" data-watch-source="${escapeHtml(instrument.source)}" data-watch-id="${instrument.sourceId}">${t("watch")}</button></td>
    </tr>
  `).join("") || `<tr><td colspan="6" class="muted">${t("searchEmpty")}</td></tr>`;
}

async function searchMarket() {
  const query = $("#marketSearch").value.trim();
  if (!query) {
    state.marketResults = [];
    renderMarketResults();
    showToast("Type a symbol, company, ETF, or currency to search.");
    return;
  }

  const button = $("#marketSearchBtn");
  if (button) setBusy(button, true);
  try {
    await loadInstruments(query);
    state.marketResults = await Promise.all(state.instruments.map((instrument) =>
      loadInstrumentQuote(instrument).catch(() => ({ ...instrument, price: null, change: null, subtitle: "Market Close" }))));
    renderMarketResults();
    showToast(`${state.marketResults.length} instruments loaded from NADPCO.`);
  } catch (error) {
    showToast(error.message);
  } finally {
    if (button) setBusy(button, false);
  }
}

function render() {
  const filtered = getFilteredHoldings();
  renderTable($("#holdingsBody"), filtered);
  renderTable($("#holdingsBodyFull"), filtered);
  renderSummary();
  renderAllocation();
  renderWatchlist();
  renderMarketResults();
}

function setScreen(screen) {
  state.activeScreen = screen;
  $$(".screen").forEach((el) => el.classList.toggle("active", el.id === `${screen}Screen`));
  $$(".nav button").forEach((btn) => btn.classList.toggle("active", btn.dataset.screen === screen));
  const titles = {
    dashboard: [t("dashboardTitle"), t("dashboardSubtitle")],
    holdings: [t("holdingsTitle"), t("holdingsSubtitle")],
    settings: [t("settingsTitle"), t("settingsSubtitle")]
  };
  $("#screenTitle").textContent = titles[screen][0];
  $("#screenSubtitle").textContent = titles[screen][1];
  $("#assetSearch").classList.toggle("hidden", screen === "settings");
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
$("#refreshWatch").addEventListener("click", (event) => refreshWatchlistQuotes(event.currentTarget));
$("#marketSearchBtn").addEventListener("click", searchMarket);
$("#marketSearch").addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    searchMarket();
  }
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

async function searchModalInstrument() {
  const value = $("#marketCompanySearch").value.trim();
  if (!value) {
    showToast(t("chooseInstrument"));
    return;
  }

  await loadInstruments(value);
  const selected = state.instruments.find((instrument) => `${instrument.symbol} - ${instrument.title}` === value || instrument.symbol === value) || state.instruments[0];
  if (!selected) {
    showToast("No matching instrument found.");
    return;
  }

  const quote = await loadInstrumentQuote(selected).catch(() => ({ ...selected, price: selected.price ?? null, change: selected.change ?? null }));
  fillAssetFormFromInstrument(quote);
  $("#marketCompanySearch").value = `${quote.symbol} - ${quote.title}`;
  showToast(quote.price == null ? `${quote.symbol}: ${t("enterManual")}` : `${quote.symbol} loaded from NADPCO.`);
}

let companySearchTimer;
$("#marketCompanySearch").addEventListener("input", (event) => {
  clearTimeout(companySearchTimer);
  const value = event.target.value.trim();
  companySearchTimer = setTimeout(async () => {
    try {
      await loadInstruments(value);
      const selected = state.instruments.find((instrument) => `${instrument.symbol} - ${instrument.title}` === value || instrument.symbol === value);
      if (!selected) return;

      const quote = await api(`/api/market/instruments/${selected.source}/${selected.sourceId}/quote`);
      const price = quote.price ?? quote.closingPrice ?? quote.lastPrice ?? selected.price;
      fillAssetFormFromInstrument({ ...selected, price });
      showToast(price == null ? `${selected.symbol}: ${t("enterManual")}` : `${selected.symbol} loaded from NADPCO.`);
    } catch (error) {
      showToast(error.message);
    }
  }, 250);
});

$("#modalInstrumentSearch").addEventListener("click", () => searchModalInstrument().catch((error) => showToast(error.message)));
$("#marketCompanySearch").addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    searchModalInstrument().catch((error) => showToast(error.message));
  }
});

const loadCurrencyValueButton = $("#loadCurrencyValue");
if (loadCurrencyValueButton) {
  loadCurrencyValueButton.addEventListener("click", async (event) => {
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
}

$("#signupPassword").addEventListener("input", (event) => {
  const score = passwordScore(event.target.value);
  $("#strengthFill").style.width = `${Math.max(8, score)}%`;
  $("#strengthFill").style.background = score > 70 ? "#25e75c" : score > 35 ? "#f2c45a" : "#ff5d70";
  $("#strengthLabel").textContent = score > 70 ? "Password Strength: Strong" : score > 35 ? "Password Strength: Medium" : "Password Strength: Weak";
});

$("#googleLogin").addEventListener("click", async () => {
  await enterPortfolioOverviewFromPlaceholder();
});

$("#googleSignup").addEventListener("click", async () => {
  await enterPortfolioOverviewFromPlaceholder();
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
$("#addAssetBtn").addEventListener("click", () => openHoldingModal());
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
      companyId: $("#assetCompanyId").value ? Number($("#assetCompanyId").value) : null,
      currencyId: $("#assetCurrencyId").value ? Number($("#assetCurrencyId").value) : null,
      purchaseDate: $("#assetDate").value || null
    };

    if (!asset.name || !asset.ticker) throw new Error(t("chooseInstrument"));
    if (!asset.shares || asset.shares <= 0) throw new Error(t("enterAmount"));
    if (!asset.price || asset.price <= 0) throw new Error(t("enterPrice"));
    await api("/api/portfolio/holdings", { method: "POST", body: JSON.stringify(asset) });
    event.target.reset();
    $("#assetModal").classList.add("hidden");
    await loadPortfolio();
    showToast(`${asset.ticker} ${t("addedHolding")}`);
  } catch (error) {
    showToast(error.message);
  } finally {
    setBusy(submit, false);
  }
});

document.addEventListener("click", async (event) => {
  const addHoldingButton = event.target.closest("[data-add-holding-source]");
  if (addHoldingButton) {
    const source = addHoldingButton.dataset.addHoldingSource;
    const sourceId = Number(addHoldingButton.dataset.addHoldingId);
    const instrument = state.marketResults.find((item) => item.source === source && item.sourceId === sourceId);
    if (instrument) openHoldingModal(instrument);
    return;
  }

  const watchButton = event.target.closest("[data-watch-source]");
  if (watchButton) {
    const source = watchButton.dataset.watchSource;
    const sourceId = Number(watchButton.dataset.watchId);
    const instrument = state.marketResults.find((item) => item.source === source && item.sourceId === sourceId);
    if (!instrument) return;

    setBusy(watchButton, true);
    try {
      await api("/api/portfolio/watchlist", {
        method: "POST",
        body: JSON.stringify({
          source: instrument.source,
          sourceId: instrument.sourceId,
          symbol: instrument.symbol,
          title: instrument.title,
          type: instrument.type,
          price: instrument.price || 0,
          change: instrument.change || 0
        })
      });
      await loadPortfolio();
      showToast(`${instrument.symbol} added to watchlist.`);
    } catch (error) {
      showToast(error.message);
    } finally {
      setBusy(watchButton, false);
    }
    return;
  }

  const openHoldingButton = event.target.closest("[data-open-holding-modal]");
  if (openHoldingButton) {
    openHoldingModal();
    return;
  }

  const removeWatchButton = event.target.closest("[data-remove-watch-source]");
  if (removeWatchButton) {
    setBusy(removeWatchButton, true);
    try {
      await api(`/api/portfolio/watchlist/${encodeURIComponent(removeWatchButton.dataset.removeWatchSource)}/${removeWatchButton.dataset.removeWatchId}`, { method: "DELETE" });
      await loadPortfolio();
      showToast("Instrument removed from watchlist.");
    } catch (error) {
      showToast(error.message);
    } finally {
      setBusy(removeWatchButton, false);
    }
    return;
  }

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

$("#languageSelect").addEventListener("change", (event) => applyLanguage(event.target.value));
applyLanguage(currentLanguage);

loadAppData()
  .then(() => {
    if (state.user) {
      $("#authShell").classList.add("hidden");
      $("#appShell").classList.remove("hidden");
      setScreen("dashboard");
    }
  });
