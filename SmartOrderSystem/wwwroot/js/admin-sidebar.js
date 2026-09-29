function initAdminSidebar() {
    const toggle = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('sidebarOverlay');

    if (!toggle || !sidebar || !overlay) {
        return;
    }

    function openSidebar() {
        sidebar.classList.add('open');
        overlay.classList.add('show');
        toggle.setAttribute('aria-expanded', 'true');
    }

    function closeSidebar() {
        sidebar.classList.remove('open');
        overlay.classList.remove('show');
        toggle.setAttribute('aria-expanded', 'false');
    }

    toggle.addEventListener('click', () =>
        sidebar.classList.contains('open') ? closeSidebar() : openSidebar()
    );

    overlay.addEventListener('click', closeSidebar);
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAdminSidebar);
} else {
    initAdminSidebar();
}

window.initAdminSidebar = initAdminSidebar;
