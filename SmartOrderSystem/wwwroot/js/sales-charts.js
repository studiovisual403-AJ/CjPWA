/* ============================================================
   Sales Detailed Report — client script
   Depends on: Chart.js (loaded in the view), window.salesDetailsInitial,
   window.salesDetailsUrls.data
   ============================================================ */

(function () {
    "use strict";

    const charts = {}; // keep Chart.js instances so we can destroy/recreate on refresh
    let currentData = window.salesDetailsInitial || null;
    let txSearchTerm = "";

    const els = {
        startDate: document.getElementById("filterStartDate"),
        endDate: document.getElementById("filterEndDate"),
        brand: document.getElementById("filterBrand"),
        category: document.getElementById("filterCategory"),
        status: document.getElementById("filterStatus"),
        generateBtn: document.getElementById("btnGenerateReport"),
        exportExcelBtn: document.getElementById("btnExportExcel"),
        exportPdfBtn: document.getElementById("btnExportPdf"),
        txSearch: document.getElementById("txSearch"),
        txTableBody: document.getElementById("txTableBody"),
        txShowingText: document.getElementById("txShowingText"),
        txPagination: document.getElementById("txPagination"),
        bestSellingTable: document.getElementById("bestSellingTable"),
        brandLegend: document.getElementById("brandLegend"),
        statusLegend: document.getElementById("statusLegend"),
    };

    // ---------- formatting helpers ----------
    const money = (n) =>
        new Intl.NumberFormat("en-PH", { style: "currency", currency: "PHP", maximumFractionDigits: 0 }).format(n || 0);

    const dateFmt = (isoOrDate) => {
        const d = new Date(isoOrDate);
        if (isNaN(d.getTime())) return "";
        return d.toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
    };

    const statusClass = (status) => {
        switch ((status || "").toLowerCase()) {
            case "completed": return "sd-status-completed";
            case "reserved": return "sd-status-reserved";
            case "cancelled": return "sd-status-cancelled";
            default: return "";
        }
    };

    const escapeHtml = (str) =>
        String(str ?? "").replace(/[&<>"']/g, (c) => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
        }[c]));

    // ---------- data fetch ----------
    function buildQuery(page) {
        const params = new URLSearchParams();
        if (els.startDate.value) params.set("startDate", els.startDate.value);
        if (els.endDate.value) params.set("endDate", els.endDate.value);
        if (els.brand.value) params.set("brand", els.brand.value);
        if (els.category.value) params.set("category", els.category.value);
        if (els.status.value) params.set("status", els.status.value);
        params.set("page", page || 1);
        return params.toString();
    }

    async function fetchReport(page) {
        setLoading(true);
        try {
            const url = `${window.salesDetailsUrls.data}?${buildQuery(page)}`;
            const res = await fetch(url, { headers: { Accept: "application/json" } });
            if (!res.ok) throw new Error(`Request failed: ${res.status}`);
            const data = await res.json();
            currentData = data;
            renderAll(data);
        } catch (err) {
            console.error("Failed to load sales report:", err);
            if (els.txTableBody) {
                els.txTableBody.innerHTML =
                    `<tr class="sd-tx-empty"><td colspan="9">Couldn't load report data. Please try again.</td></tr>`;
            }
        } finally {
            setLoading(false);
        }
    }

    function setLoading(isLoading) {
        if (els.generateBtn) els.generateBtn.disabled = isLoading;
        document.getElementById("kpiGrid")?.classList.toggle("sd-loading", isLoading);
    }

    // ---------- render orchestration ----------
    function renderAll(data) {
        renderKpis(data);
        renderMonthlyTrendChart(data.monthlySalesTrend || []);
        renderBrandDonut(data.brandSales || []);
        renderCategoryBar(data.categorySales || []);
        renderStatusDonut(data.orderStatusDistribution || []);
        renderBestSelling(data.bestSellingProducts || []);
        renderTransactions(data);
    }

    // ---------- KPI cards ----------
    function renderKpis(data) {
        setKpi("kpiTotalSales", money(data.totalSales));
        setGrowth("kpiTotalSalesGrowth", data.totalSalesGrowth);

        setKpi("kpiCompletedOrders", data.completedOrders);
        setGrowth("kpiCompletedOrdersGrowth", data.completedOrdersGrowth);

        setKpi("kpiProductsSold", data.totalProductsSold);
        setGrowth("kpiProductsSoldGrowth", data.totalProductsSoldGrowth);

        setKpi("kpiAvgOrderValue", money(data.averageOrderValue));
        setGrowth("kpiAvgOrderValueGrowth", data.averageOrderValueGrowth);

        setKpi("kpiBestProduct", data.bestSellingProductName || "N/A");
        const unitsEl = document.getElementById("kpiBestProductUnits");
        if (unitsEl) unitsEl.textContent = `${data.bestSellingProductUnits || 0} units sold`;
    }

    function setKpi(id, value) {
        const el = document.getElementById(id);
        if (el) el.textContent = value;
    }

    function setGrowth(id, value) {
        const el = document.getElementById(id);
        if (!el) return;
        const v = Number(value) || 0;
        const up = v >= 0;
        el.classList.toggle("up", up);
        el.classList.toggle("down", !up);
        const icon = el.querySelector("i");
        if (icon) icon.className = `ti ${up ? "ti-arrow-up-right" : "ti-arrow-down-right"}`;
        el.childNodes[el.childNodes.length - 1].textContent = ` ${Math.abs(v)}% from last month`;
    }

    // ---------- charts ----------
    function destroyChart(key) {
        if (charts[key]) {
            charts[key].destroy();
            delete charts[key];
        }
    }

    function renderMonthlyTrendChart(rows) {
        const canvas = document.getElementById("monthlyTrendChart");
        if (!canvas) return;
        destroyChart("trend");

        charts.trend = new Chart(canvas.getContext("2d"), {
            type: "line",
            data: {
                labels: rows.map((r) => r.month),
                datasets: [{
                    label: "Sales",
                    data: rows.map((r) => r.amount),
                    borderColor: "#3b82f6",
                    backgroundColor: "rgba(59, 130, 246, 0.12)",
                    tension: 0.35,
                    fill: true,
                    pointRadius: 3,
                    pointBackgroundColor: "#3b82f6",
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: (ctx) => money(ctx.parsed.y) } },
                },
                scales: {
                    y: { ticks: { callback: (v) => money(v) }, grid: { color: "#f1f5f9" } },
                    x: { grid: { display: false } },
                },
            },
        });
        canvas.parentElement.style.height = "260px";
    }

    function renderBrandDonut(rows) {
        const canvas = document.getElementById("brandDonutChart");
        if (!canvas) return;
        destroyChart("brand");

        charts.brand = new Chart(canvas.getContext("2d"), {
            type: "doughnut",
            data: {
                labels: rows.map((r) => r.brand),
                datasets: [{
                    data: rows.map((r) => r.revenue),
                    backgroundColor: rows.map((r) => r.color),
                    borderWidth: 0,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "68%",
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: (ctx) => `${ctx.label}: ${money(ctx.parsed)}` } },
                },
            },
        });

        renderLegend(els.brandLegend, rows.map((r) => ({
            label: r.brand,
            color: r.color,
            extra: `${r.percentage.toFixed(0)}%`,
        })));
    }

    function renderCategoryBar(rows) {
        const canvas = document.getElementById("categoryBarChart");
        if (!canvas) return;
        destroyChart("category");

        charts.category = new Chart(canvas.getContext("2d"), {
            type: "bar",
            data: {
                labels: rows.map((r) => r.category),
                datasets: [{
                    label: "Sales",
                    data: rows.map((r) => r.amount),
                    backgroundColor: "#8b5cf6",
                    borderRadius: 6,
                    maxBarThickness: 36,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: (ctx) => money(ctx.parsed.y) } },
                },
                scales: {
                    y: { ticks: { callback: (v) => money(v) }, grid: { color: "#f1f5f9" } },
                    x: { grid: { display: false } },
                },
            },
        });
    }

    function renderStatusDonut(rows) {
        const canvas = document.getElementById("statusDonutChart");
        if (!canvas) return;
        destroyChart("status");

        charts.status = new Chart(canvas.getContext("2d"), {
            type: "doughnut",
            data: {
                labels: rows.map((r) => r.status),
                datasets: [{
                    data: rows.map((r) => r.count),
                    backgroundColor: rows.map((r) => r.color),
                    borderWidth: 0,
                }],
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: "68%",
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: (ctx) => `${ctx.label}: ${ctx.parsed} (${rows[ctx.dataIndex].percentage.toFixed(0)}%)` } },
                },
            },
        });

        renderLegend(els.statusLegend, rows.map((r) => ({
            label: r.status,
            color: r.color,
            extra: `${r.count}`,
        })));
    }

    function renderLegend(container, items) {
        if (!container) return;
        container.innerHTML = items.map((it) => `
            <span class="sd-legend-item">
                <span class="sd-legend-dot" style="background:${it.color}"></span>
                ${escapeHtml(it.label)} &middot; ${escapeHtml(it.extra)}
            </span>
        `).join("");
    }

    // ---------- best selling products ----------
    function renderBestSelling(rows) {
        if (!els.bestSellingTable) return;
        if (!rows.length) {
            els.bestSellingTable.innerHTML = `<div class="sd-tx-empty" style="padding:24px;text-align:center;color:#94a3b8;">No data for this period.</div>`;
            return;
        }
        els.bestSellingTable.innerHTML = rows.map((p) => `
            <div class="sd-product-row">
                <span class="sd-product-rank">${p.rank}</span>
                <img class="sd-product-thumb" src="${escapeHtml(p.imageUrl || "/images/placeholder.png")}" alt="${escapeHtml(p.productName)}" onerror="this.style.visibility='hidden'" />
                <div class="sd-product-info">
                    <div class="sd-product-name">${escapeHtml(p.productName)}</div>
                    <div class="sd-product-meta">${escapeHtml(p.brand)} &middot; ${escapeHtml(p.category)}</div>
                </div>
                <div class="sd-product-stats">
                    <div class="sd-product-units">${p.unitsSold} sold</div>
                    <div class="sd-product-revenue">${money(p.revenue)}</div>
                </div>
            </div>
        `).join("");
    }

    // ---------- transactions table + pagination ----------
    function renderTransactions(data) {
        const rows = data.transactions || [];
        const filtered = txSearchTerm
            ? rows.filter((t) =>
                t.orderCode.toLowerCase().includes(txSearchTerm) ||
                t.customerName.toLowerCase().includes(txSearchTerm) ||
                t.productName.toLowerCase().includes(txSearchTerm))
            : rows;

        if (!els.txTableBody) return;

        if (!filtered.length) {
            els.txTableBody.innerHTML = `<tr class="sd-tx-empty"><td colspan="9">No transactions found.</td></tr>`;
        } else {
            els.txTableBody.innerHTML = filtered.map((t) => `
                <tr>
                    <td>${escapeHtml(t.orderCode)}</td>
                    <td>${escapeHtml(t.customerName)}</td>
                    <td>${escapeHtml(t.productName)}</td>
                    <td>${escapeHtml(t.category)}</td>
                    <td>${t.qty}</td>
                    <td>${money(t.amount)}</td>
                    <td>${dateFmt(t.date)}</td>
                    <td><span class="sd-status-badge ${statusClass(t.status)}">${escapeHtml(t.status)}</span></td>
                    <td>${escapeHtml(t.paymentMethod || "—")}</td>
                </tr>
            `).join("");
        }

        const start = data.totalTransactions === 0 ? 0 : (data.currentPage - 1) * data.pageSize + 1;
        const end = Math.min(data.currentPage * data.pageSize, data.totalTransactions);
        if (els.txShowingText) {
            els.txShowingText.textContent = txSearchTerm
                ? `Showing ${filtered.length} matching entries (page ${data.currentPage})`
                : `Showing ${start}-${end} of ${data.totalTransactions} entries`;
        }

        renderPagination(data.currentPage, data.totalPages);
    }

    function renderPagination(current, totalPages) {
        if (!els.txPagination) return;
        if (totalPages <= 1) {
            els.txPagination.innerHTML = "";
            return;
        }

        const buttons = [];
        buttons.push(pageBtn("«", current - 1, current === 1));

        const windowSize = 2;
        const pages = new Set([1, totalPages]);
        for (let p = current - windowSize; p <= current + windowSize; p++) {
            if (p >= 1 && p <= totalPages) pages.add(p);
        }
        const sorted = Array.from(pages).sort((a, b) => a - b);

        let prev = 0;
        for (const p of sorted) {
            if (prev && p - prev > 1) buttons.push(`<span class="sd-page-btn" style="border:none;cursor:default;">…</span>`);
            buttons.push(pageBtn(String(p), p, false, p === current));
            prev = p;
        }

        buttons.push(pageBtn("»", current + 1, current === totalPages));
        els.txPagination.innerHTML = buttons.join("");

        els.txPagination.querySelectorAll("button[data-page]").forEach((btn) => {
            btn.addEventListener("click", () => fetchReport(Number(btn.dataset.page)));
        });
    }

    function pageBtn(label, page, disabled, active) {
        return `<button type="button" class="sd-page-btn${active ? " active" : ""}" data-page="${page}" ${disabled ? "disabled" : ""}>${label}</button>`;
    }

    // ---------- export ----------
    function exportCsv() {
        if (!currentData) return;
        const rows = currentData.transactions || [];
        const header = ["Order ID", "Customer", "Product", "Category", "Qty", "Amount", "Date", "Status", "Payment"];
        const lines = [header.join(",")];
        rows.forEach((t) => {
            const line = [
                t.orderCode, t.customerName, t.productName, t.category,
                t.qty, t.amount, dateFmt(t.date), t.status, t.paymentMethod || "",
            ].map((v) => `"${String(v).replace(/"/g, '""')}"`).join(",");
            lines.push(line);
        });

        const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `sales-report-${currentData.dateStart?.slice(0, 10)}_to_${currentData.dateEnd?.slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    }

    function exportPdf() {
        if (!currentData) return;
        const params = new URLSearchParams({
            startDate: currentData.dateStart?.slice(0, 10) || "",
            endDate: currentData.dateEnd?.slice(0, 10) || "",
            brand: currentData.selectedBrand || "",
            category: currentData.selectedCategory || "",
            status: currentData.selectedStatus || ""
        });
        window.location.href = `/Sales/ExportPdf?${params.toString()}`;
    }

    // ---------- events ----------
    function bindEvents() {
        els.generateBtn?.addEventListener("click", () => fetchReport(1));  
        els.exportExcelBtn?.addEventListener("click", exportCsv);
        els.exportPdfBtn?.addEventListener("click", exportPdf);

        let searchTimer;
        els.txSearch?.addEventListener("input", (e) => {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(() => {
                txSearchTerm = e.target.value.trim().toLowerCase();
                if (currentData) renderTransactions(currentData);
            }, 200);
        });

        [els.brand, els.category, els.status].forEach((el) => {
            el?.addEventListener("change", () => fetchReport(1));
        });
    }

    // ---------- init ----------
    document.addEventListener("DOMContentLoaded", () => {
        bindEvents();
        if (currentData) {
            renderAll(currentData);
        } else {
            fetchReport(1);
        }
    });
})();