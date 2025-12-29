// Premium Smooth Scrolling and Interactions
(function() {
    'use strict';

    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            if (href !== '#' && href.length > 1) {
                e.preventDefault();
                const target = document.querySelector(href);
                if (target) {
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            }
        });
    });

    // Add smooth fade-in on scroll
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver(function(entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
            }
        });
    }, observerOptions);

    // Observe all cards for scroll animations
    document.querySelectorAll('.card').forEach(card => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        card.style.transition = 'opacity 0.6s cubic-bezier(0.4, 0, 0.2, 1), transform 0.6s cubic-bezier(0.4, 0, 0.2, 1)';
        observer.observe(card);
    });

    // Premium button ripple effect
    document.querySelectorAll('.btn').forEach(button => {
        button.addEventListener('click', function(e) {
            const ripple = document.createElement('span');
            const rect = this.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;
            
            ripple.style.width = ripple.style.height = size + 'px';
            ripple.style.left = x + 'px';
            ripple.style.top = y + 'px';
            ripple.classList.add('ripple');
            
            this.appendChild(ripple);
            
            setTimeout(() => {
                ripple.remove();
            }, 600);
        });
    });

    // Navbar hide on scroll down, show on scroll up
    let lastScroll = 0;
    let scrollThreshold = 100;
    const navbar = document.querySelector('.navbar');
    
    window.addEventListener('scroll', () => {
        const currentScroll = window.pageYOffset;
        
        // Update shadow
        if (currentScroll > 50) {
            navbar.style.boxShadow = '0 4px 16px rgba(0, 0, 0, 0.08)';
        } else {
            navbar.style.boxShadow = '0 2px 8px rgba(0, 0, 0, 0.04)';
        }
        
        // Hide/show navbar based on scroll direction
        if (currentScroll > scrollThreshold) {
            if (currentScroll > lastScroll) {
                // Scrolling down - hide navbar
                navbar.classList.add('navbar-hidden');
            } else {
                // Scrolling up - show navbar
                navbar.classList.remove('navbar-hidden');
            }
        } else {
            // Near top - always show navbar
            navbar.classList.remove('navbar-hidden');
        }
        
        lastScroll = currentScroll;
    }, { passive: true });

    // Form input focus animations
    document.querySelectorAll('.form-control, .form-select').forEach(input => {
        input.addEventListener('focus', function() {
            this.parentElement?.classList.add('focused');
        });
        
        input.addEventListener('blur', function() {
            this.parentElement?.classList.remove('focused');
        });
    });

    // Prevent form submission animation glitches
    document.querySelectorAll('form').forEach(form => {
        form.addEventListener('submit', function(e) {
            const submitBtn = this.querySelector('button[type="submit"]');
            if (submitBtn && !submitBtn.disabled) {
                submitBtn.style.transform = 'scale(0.98)';
                setTimeout(() => {
                    submitBtn.style.transform = '';
                }, 150);
            }
        });
    });

    // Mobile-specific enhancements
    const isMobile = /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);
    const isTouchDevice = 'ontouchstart' in window || navigator.maxTouchPoints > 0;

    if (isMobile || isTouchDevice) {
        // Prevent double-tap zoom on buttons and cards
        let lastTouchEnd = 0;
        document.addEventListener('touchend', function(event) {
            const now = Date.now();
            if (now - lastTouchEnd <= 300) {
                event.preventDefault();
            }
            lastTouchEnd = now;
        }, false);

        // Add touch feedback to interactive elements
        document.querySelectorAll('.btn, .card, .page-link, .form-control, .form-select').forEach(element => {
            element.addEventListener('touchstart', function() {
                this.style.opacity = '0.7';
            }, { passive: true });
            
            element.addEventListener('touchend', function() {
                setTimeout(() => {
                    this.style.opacity = '';
                }, 150);
            }, { passive: true });
        });

        // Improve autocomplete for mobile
        document.querySelectorAll('.ui-autocomplete').forEach(autocomplete => {
            autocomplete.style.position = 'fixed';
            autocomplete.style.maxWidth = '90vw';
        });

        // Optimize scroll performance on mobile
        let ticking = false;
        window.addEventListener('scroll', function() {
            if (!ticking) {
                window.requestAnimationFrame(function() {
                    // Scroll handling code here
                    ticking = false;
                });
                ticking = true;
            }
        }, { passive: true });

        // Prevent pull-to-refresh on mobile (iOS Safari)
        let touchStartY = 0;
        document.addEventListener('touchstart', function(e) {
            touchStartY = e.touches[0].clientY;
        }, { passive: true });

        document.addEventListener('touchmove', function(e) {
            if (window.scrollY === 0 && e.touches[0].clientY > touchStartY) {
                e.preventDefault();
            }
        }, { passive: false });

        // Add safe area padding for notched devices
        const setSafeAreaPadding = () => {
            const safeAreaTop = getComputedStyle(document.documentElement).getPropertyValue('env(safe-area-inset-top)');
            const safeAreaBottom = getComputedStyle(document.documentElement).getPropertyValue('env(safe-area-inset-bottom)');
            
            if (safeAreaTop || safeAreaBottom) {
                document.documentElement.style.setProperty('--safe-area-top', safeAreaTop || '0px');
                document.documentElement.style.setProperty('--safe-area-bottom', safeAreaBottom || '0px');
            }
        };

        setSafeAreaPadding();
        window.addEventListener('resize', setSafeAreaPadding);
        window.addEventListener('orientationchange', setSafeAreaPadding);

        // Improve form input experience on mobile
        document.querySelectorAll('input[type="date"], input[type="text"], select').forEach(input => {
            input.addEventListener('focus', function() {
                // Scroll input into view on mobile
                setTimeout(() => {
                    this.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }, 300);
            });
        });

        // Optimize card animations for mobile (reduce motion if needed)
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            document.querySelectorAll('.card, .flight-card').forEach(card => {
                card.style.transition = 'none';
            });
        }
    }

    // Viewport height fix for mobile browsers
    const setViewportHeight = () => {
        const vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    };

    setViewportHeight();
    window.addEventListener('resize', setViewportHeight);
    window.addEventListener('orientationchange', setViewportHeight);
})();
