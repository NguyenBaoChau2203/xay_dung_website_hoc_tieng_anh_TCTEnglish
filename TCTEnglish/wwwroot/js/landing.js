(function () {
  "use strict";

  var header = document.getElementById("landingHeader");
  var menuToggle = document.getElementById("landingMenuToggle");
  var drawer = document.getElementById("landingDrawer");
  var drawerPanel = drawer ? drawer.querySelector(".landing-drawer__panel") : null;
  var lastFocus = null;
  var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  function setHeaderScrolled() {
    if (!header) return;
    header.classList.toggle("is-scrolled", window.scrollY > 16);
  }

  function getFocusable(container) {
    if (!container) return [];
    return Array.prototype.slice.call(
      container.querySelectorAll(
        'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])'
      )
    ).filter(function (el) {
      return !el.hasAttribute("disabled") && el.getAttribute("aria-hidden") !== "true";
    });
  }

  function openDrawer() {
    if (!drawer || !menuToggle) return;
    lastFocus = document.activeElement;
    drawer.classList.add("is-open");
    drawer.setAttribute("aria-hidden", "false");
    menuToggle.setAttribute("aria-expanded", "true");
    document.body.classList.add("landing-menu-open");
    var focusables = getFocusable(drawerPanel);
    if (focusables.length) focusables[0].focus();
  }

  function closeDrawer() {
    if (!drawer || !menuToggle) return;
    drawer.classList.remove("is-open");
    drawer.setAttribute("aria-hidden", "true");
    menuToggle.setAttribute("aria-expanded", "false");
    document.body.classList.remove("landing-menu-open");
    if (lastFocus && typeof lastFocus.focus === "function") {
      lastFocus.focus();
    } else {
      menuToggle.focus();
    }
  }

  function isDrawerOpen() {
    return drawer && drawer.classList.contains("is-open");
  }

  setHeaderScrolled();
  window.addEventListener("scroll", setHeaderScrolled, { passive: true });

  if (menuToggle && drawer) {
    menuToggle.addEventListener("click", function () {
      if (isDrawerOpen()) closeDrawer();
      else openDrawer();
    });

    drawer.addEventListener("click", function (event) {
      if (event.target === drawer) closeDrawer();
    });

    drawer.querySelectorAll("a").forEach(function (link) {
      link.addEventListener("click", closeDrawer);
    });

    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && isDrawerOpen()) {
        event.preventDefault();
        closeDrawer();
        return;
      }

      if (event.key !== "Tab" || !isDrawerOpen() || !drawerPanel) return;

      var focusables = getFocusable(drawerPanel);
      if (!focusables.length) return;

      var first = focusables[0];
      var last = focusables[focusables.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });
  }

  document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
    anchor.addEventListener("click", function (event) {
      var id = anchor.getAttribute("href");
      if (!id || id === "#") return;
      var target = document.querySelector(id);
      if (!target) return;
      event.preventDefault();
      target.scrollIntoView({
        behavior: reduceMotion ? "auto" : "smooth",
        block: "start"
      });
      if (history.replaceState) {
        history.replaceState(null, "", id);
      }
    });
  });

  if (!reduceMotion && "IntersectionObserver" in window) {
    var reveals = document.querySelectorAll(".landing-reveal");
    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        });
      },
      { threshold: 0.12, rootMargin: "0px 0px -40px 0px" }
    );
    reveals.forEach(function (el) {
      observer.observe(el);
    });
  } else {
    document.querySelectorAll(".landing-reveal").forEach(function (el) {
      el.classList.add("is-visible");
    });
  }
})();
