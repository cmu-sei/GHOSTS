$(document).ready(function () {

    var $button = $('#load-more-button');

    var page_num = 1;
    var max_pages = 2;
    var next_link = $button.data('load-link');

    var containerID = $button.data('container');

    var $container = $('#' + containerID );
    var container_has_isotope = false;


    if (page_num > max_pages) {
        $button.addClass('hidden-xs-up');
    }

    $button.on('click', function () {

        if (page_num <= max_pages && !$(this).hasClass('loading') && !$(this).hasClass('last-page')) {

            $.ajax({
                type: 'GET',
                url: next_link,
                dataType: 'html',
                beforeSend: function () {
                            $button.addClass('loading');
                },
                complete: function (XMLHttpRequest) {
					$button.removeClass('loading');

					if (XMLHttpRequest.status == 200 && XMLHttpRequest.responseText != '') {

						page_num++;

						if (page_num > max_pages) {
							$button.addClass('hidden-xs-up');
						}

						if ($(XMLHttpRequest.responseText).length > 0) {
							container_has_isotope = (typeof($container.isotope) === 'function');

							$(XMLHttpRequest.responseText).each(function () {
								var elem = $(this);
								if (!container_has_isotope) {
									$container.append(elem);
								} else {
									$container.imagesLoaded( function() {
										// init Isotope
										$container.isotope();
										// isotope has been initalized, okay to call methods
										$container.append( elem )
											.isotope( 'appended', elem )
											.isotope('layout');
									});
								}


							});
						}
					}
				}
            });
        }
        return false;
    });
});
