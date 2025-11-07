//Global var to avoid any conflicts
var CRUMINA = {};

(function ($) {

	// USE STRICT
	"use strict";

	//----------------------------------------------------/
	// Predefined Variables
	//----------------------------------------------------/
	var $window = $(window),
		$document = $(document),
		$body = $('body'),
		$sidebar = $('.fixed-sidebar'),
		$preloader = $('#hellopreloader');

	/* -----------------------
	 * Preloader
	 * --------------------- */

	CRUMINA.preloader = function () {
		$window.scrollTop(0);
		$preloader.addClass('active');
		setTimeout(function () {
			$preloader.fadeOut(800);
		}, 500);
		return false;
	};

	//Scroll to top.
	jQuery('.back-to-top').on('click', function () {
		$('html,body').animate({
			scrollTop: 0
		}, 1200);
		return false;
	});


	/* -----------------------
	* Input Number Quantity
	   * --------------------- */

	$(document).on("click", ".quantity-plus", function () {
		var val = parseInt($(this).prev('input').val());
		$(this).prev('input').val(val + 1).change();
		return false;
	});

	$(document).on("click", ".quantity-minus", function () {
		var val = parseInt($(this).next('input').val());
		if (val !== 1) {
			$(this).next('input').val(val - 1).change();
		}
		return false;
	});


	/* -----------------------------
	 Custom input type="number"
	 https://bootsnipp.com/snippets/featured/bootstrap-number-spinner-on-click-hold
	 * ---------------------------*/

	$(function () {
		var action;
		$(document).on("touchstart mousedown", ".number-spinner button", function () {
			var btn = $(this);
			var input = btn.closest('.number-spinner').find('input');
			btn.closest('.number-spinner').find('button').prop("disabled", false);

			if (btn.attr('data-dir') == 'up') {
				action = setInterval(function () {
					if (input.attr('max') == undefined || parseInt(input.val()) < parseInt(input.attr('max'))) {
						input.val(parseInt(input.val()) + 1);
					} else {
						btn.prop("disabled", true);
						clearInterval(action);
					}
				}, 50);
			} else {
				action = setInterval(function () {
					if (input.attr('min') == undefined || parseInt(input.val()) > parseInt(input.attr('min'))) {
						input.val(parseInt(input.val()) - 1);
					} else {
						btn.prop("disabled", true);
						clearInterval(action);
					}
				}, 50);
			}
		});
		$(document).on("touchend mouseup", ".number-spinner button", function () {
			clearInterval(action);
		});
	});

	/* -----------------------------
	 * Toggle functions
	 * ---------------------------*/

	$('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
		var target = $(e.target).attr("href"); // activated tab
		if ('#events' === target) {
			$('.fc-state-active').click();
		}
	});

	// Toggle aside panels
	$(".js-sidebar-open").on('click', function () {
		var mobileWidthApp = $('body').outerWidth();
		if (mobileWidthApp <= 560) {
			$(this).closest('body').find('.popup-chat-responsive').removeClass('open-chat');
		}

		var $currentSidebar = $(this).closest($sidebar);

		if($currentSidebar.hasClass('open')){
			$sidebar.each(function () {
				var $self = $(this);
				$self.removeClass('open');
				$self.find('.js-sidebar-open').removeClass('active');
			});
		}else {
			$sidebar.each(function () {
				var $self = $(this);
				$self.removeClass('open');
				$self.find('.js-sidebar-open').removeClass('active');
			});
			$currentSidebar.addClass('open');
			$currentSidebar.find('.js-sidebar-open').addClass('active');
		}


		return false;
	});

	// Close on "Esc" click
	$window.keydown(function (eventObject) {
		if (eventObject.which == 27 && $sidebar.is(':visible')) {
			$sidebar.removeClass('open');
		}
	});

	// Close on click outside elements.
	$document.on('click', function (event) {
		if (!$(event.target).closest($sidebar).length && $sidebar.is(':visible')) {
			$sidebar.removeClass('open');
		}
	});

	// Toggle inline popups

	var $popup = $('.window-popup');

	$(".js-open-popup").on('click', function (event) {
		var target_popup = $(this).data('popup-target');
		var current_popup = $popup.filter(target_popup);
		var offset = $(this).offset();
		current_popup.addClass('open');
		current_popup.css('top', (offset.top - (current_popup.innerHeight() / 2)));
		$body.addClass('overlay-enable');
		return false;
	});

	// Close on "Esc" click
	$window.keydown(function (eventObject) {
		if (eventObject.which == 27) {
			$popup.removeClass('open');
			$body.removeClass('overlay-enable');
			$('.profile-menu').removeClass('expanded-menu');
			$('.popup-chat-responsive').removeClass('open-chat');
			$('.profile-settings-responsive').removeClass('open');
			$('.header-menu').removeClass('open');
			$('.js-sidebar-open').removeClass('active');
		}
	});

	// Close on click outside elements.
	$document.on('click', function (event) {
		if (!$(event.target).closest($popup).length) {
			$popup.removeClass('open');
			$body.removeClass('overlay-enable');
		}

		if (!$(event.target).closest('.header-menu').length) {
			$('.header-menu').removeClass('open');
		}

		if (!$(event.target).closest('.profile-menu').length) {
			$('.profile-menu').removeClass('expanded-menu');
		}

		if (!$(event.target).closest('.profile-settings-responsive').length) {
			$('.profile-settings-responsive').removeClass('open');
		}
	});

	// Close active tab on second click.
	$('[data-toggle=tab]').on('click', function () {
		/*$body.toggleClass('body--fixed');*/
		if ($(this).hasClass('active') && $(this).closest('ul').hasClass('mobile-notification-tabs')) {
			$($(this).attr("href")).toggleClass('active');
			$(this).removeClass('active');
			return false;
		}
	});


	// Close on "X" click
	$(".js-close-popup").on('click', function () {
		$(this).closest($popup).removeClass('open');
		$body.removeClass('overlay-enable');
		return false
	});

	$(".profile-settings-open").on('click', function () {
		$('.profile-settings-responsive').toggleClass('open');
		return false
	});

	$(".js-expanded-menu").on('click', function () {
		$('.header-menu').toggleClass('expanded-menu');
		return false
	});

	$(".js-chat-open").on('click', function () {
		$('.popup-chat-responsive').toggleClass('open-chat');
		return false
	});
	$(".js-chat-close").on('click', function () {
		$('.popup-chat-responsive').removeClass('open-chat');
		return false
	});

	$(".js-open-responsive-menu").on('click', function () {
		$('.header-menu').toggleClass('open');
		return false
	});

	$(".js-close-responsive-menu").on('click', function () {
		$('.header-menu').removeClass('open');
		return false
	});


	CRUMINA.perfectScrollbarInit = function () {
		var $chatContainer = $('.popup-chat .mCustomScrollbar');
		var $containers = $('.mCustomScrollbar');

		$containers.perfectScrollbar({wheelPropagation: false});

		if (!$chatContainer.length) {
			return;
		}

		$chatContainer.scrollTop($chatContainer.prop("scrollHeight"));
		$chatContainer.perfectScrollbar('update');
	};


	/* -----------------------------
 * Responsive
 * ---------------------------*/
	CRUMINA.responsive = {
		$profilePanel: null,
		$desktopContainerPanel: null,
		$responsiveContainerPanel: null,
		init: function () {
			this.$profilePanel = jQuery('#profile-panel');
			this.$desktopContainerPanel = jQuery('#desktop-container-panel > .ui-block');
			this.$responsiveContainerPanel = jQuery('#responsive-container-panel .ui-block');
			this.update();
		},
		mixPanel: function () {
			if (window.matchMedia("(max-width: 1024px)").matches) {
				this.$responsiveContainerPanel.append(this.$profilePanel);

			} else {
				this.$desktopContainerPanel.append(this.$profilePanel);
			}
		},
		update: function () {
			var _this = this;
			var resizeTimer = null;
			var resize = function () {
				resizeTimer = null;

				// Methods
				_this.mixPanel();
			};

			$(window).on('resize', function () {
				if (resizeTimer === null) {
					resizeTimer = window.setTimeout(function () {
						resize();
					}, 300);
				}
			}).resize();
		}
	};

	/* ==========================================

	Sticky Header

	========================================== */

	CRUMINA.fixedHeader = function () {
		var $stickyHeader = $('#header--standard');

		$(document).on("scroll", function(){

			if ($(document).scrollTop() > 100){
				$stickyHeader.addClass("header--fixed");
			} else {
				$stickyHeader.removeClass("header--fixed");
			}

		});

	};

	/* -----------------------------
	 * On DOM ready functions
	 * ---------------------------*/

	$document.ready(function () {

		//CRUMINA.preloader();

		CRUMINA.perfectScrollbarInit();
		CRUMINA.fixedHeader();

		// Run scripts only if they included on page.

		if (typeof $.fn.gifplayer !== 'undefined') {
			$('.gif-play-image').gifplayer();
		}
		if (typeof $.fn.mediaelementplayer !== 'undefined') {
			$('#mediaplayer').mediaelementplayer({
				"features": ['prevtrack', 'playpause', 'nexttrack', 'loop', 'shuffle', 'current', 'progress', 'duration', 'volume']
			});
		}

		CRUMINA.responsive.init();

	});
})(jQuery);