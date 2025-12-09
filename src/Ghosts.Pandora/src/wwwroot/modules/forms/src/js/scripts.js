if ( typeof ( CRUMINA ) === 'undefined' ) {
    var CRUMINA = { };
}
CRUMINA.submit = {
    $forms: null,
    init: function () {
        this.$forms = jQuery( 'form.crumina-submit' );

        this.addEventListeners();
    },
    addEventListeners: function () {
        var _this = this;

        this.$forms.each( function () {
            var $self = jQuery( this );

            $self.on( 'submit', function ( event ) {
                event.preventDefault();
            } );

            $self.validate( {
                submitHandler: function () {
                    _this.run( $self );
                }
            } );
        } );
    },
    run: function ( $form ) {
        jQuery.ajax( {
            url: $form.attr( 'action' ),
            dataType: 'json',
            type: 'POST',
            data: {
                nonce: $form.data( 'nonce' ),
                type: $form.data( 'type' ),
                inputs: $form.serialize()
            },
            success: function ( response ) {
                if ( response.success ) {
                    $form[0].reset();
                    swal( {
                        title: 'Success!',
                        text: response.message,
                        type: 'success'
                    } );
                } else {
                    swal( {
                        title: 'Error!',
                        text: response.message,
                        type: 'error'
                    } );
                }
            },
            error: function ( jqXHR, textStatus ) {
                swal( {
                    title: 'Error!',
                    text: textStatus,
                    type: 'error'
                } );
            }
        } );
    }
};

jQuery( document ).ready( function () {
    CRUMINA.submit.init();
} );