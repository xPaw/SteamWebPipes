( function()
{
	const container = document.querySelector( '#js-realtime-log' );
	const usersOnline = document.querySelector( '#js-realtime-users' );

	container.innerHTML = '';

	const GetTime = function()
	{
		const date = new Date();
		let hh = date.getUTCHours();
		let mm = date.getUTCMinutes();
		let ss = date.getSeconds();

		if( hh < 10 )
		{
			hh = '0' + hh;
		}
		if( mm < 10 )
		{
			mm = '0' + mm;
		}
		if( ss < 10 )
		{
			ss = '0' + ss;
		}

		return hh + ':' + mm + ':' + ss;
	};

	const AddToLog = function( text )
	{
		const element = document.createElement( 'div' );
		element.className = 'line';

		const time = document.createElement( 'span' );
		time.textContent = GetTime();
		time.className = 'time';
		element.appendChild( time );

		element.insertAdjacentHTML( 'beforeend', text );

		container.prepend( element );

		// Keep only 1000 log lines
		for( let i = container.childNodes.length - 1; i > 1000; i-- )
		{
			container.childNodes[ i ].remove();
		}
	};

	AddToLog( 'Initializing… Powered by <a href="https://github.com/xPaw/SteamWebPipes">SteamWebPipes</a>' );

	let reconnectAttempts = 1;

	function generateInterval( k )
	{
		const maxInterval = Math.pow( 2, k ) * 1000;

		return 5000 * Math.random() + maxInterval;
	}

	function createWebSocket()
	{
		const connection = new WebSocket( 'ws://localhost:8181', [ 'steam-pics' ] );

		connection.onopen = function()
		{
			reconnectAttempts = 1;

			AddToLog( 'Connection opened, listening for changes…' );
		};

		connection.onclose = function()
		{
			usersOnline.textContent = '?';

			if( reconnectAttempts > 7 )
			{
				AddToLog( 'Unable to initialize connect to backend server, will no longer retry. Refresh the page manually.' );

				return;
			}

			const time = generateInterval( reconnectAttempts );

			AddToLog( 'Connection dropped, retrying in ' + Math.round( time / 1000 ) + ' seconds…' );

			setTimeout( function()
			{
				reconnectAttempts++;
				createWebSocket();
			}, time );
		};

		connection.onmessage = function( e )
		{
			const data = JSON.parse( e.data );

			switch( data.Type )
			{
				case 'UsersOnline':
				{
					usersOnline.textContent = data.Users;
					break;
				}

				case 'LogOn':
				{
					AddToLog( 'Bot logged on to Steam, checking for new changelists…' );
					break;
				}

				case 'LogOff':
				{
					AddToLog( 'Bot logged off from Steam, reconnecting soon…' );
					break;
				}

				case 'Changelist':
				{
					let str = 'Changelist <a href="/changelist/' + data.ChangeNumber + '/" class="muted" rel="nofollow">#' + data.ChangeNumber + '</a>';

					let list = [];

					for( let [ appid, value ] of Object.entries( data.Apps ) )
					{
						appid = +appid;
						value = value.replace( /&/g, '&amp;' ).replace( /</g, '&lt;' ).replace( />/g, '&gt;' );
						list.push( `<a href="/app/${appid}/history/" target="_blank" rel="noopener">${value}</a>` );
					}

					if( list.length )
					{
						str += ' — Apps (' + list.length + '): ' + list.join( ', ' );
					}

					list = [];

					for( let [ subid, value ] of Object.entries( data.Packages ) )
					{
						subid = +subid;
						value = value.replace( /&/g, '&amp;' ).replace( /</g, '&lt;' ).replace( />/g, '&gt;' );
						list.push( `<a href="/sub/${subid}/history/" target="_blank" rel="noopener">${value}</a>` );
					}

					if( list.length )
					{
						str += ' — Packages (' + list.length + '): ' + list.join( ', ' );
					}

					AddToLog( str );

					break;
				}

				default:
				{
					AddToLog( 'Received unknown event ' + data.Type );
				}
			}
		};
	}

	createWebSocket();
}() );
