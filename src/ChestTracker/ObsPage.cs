namespace ChestTracker;

/// <summary>
/// The OBS Browser Source page: the overlay's look (Classic chest, outlined Bahnschrift figures) on a transparent
/// background, refreshed every second from /overlay.json. Options in the address: ?align=left, ?scale=1.5.
/// </summary>
public static class ObsPage
{
    public const string Html = """
        <!doctype html>
        <html><head><meta charset="utf-8"><title>chest-tracker overlay</title>
        <style>
        html, body { margin: 0; background: transparent; overflow: hidden; }
        /* Bahnschrift is a variable font: weight 600 + width 75 % = the overlay's "SemiBold Condensed". */
        body { font-family: "Bahnschrift", "Arial Narrow", sans-serif; font-weight: 600; font-stretch: 75%; color: #fff;
               display: flex; flex-direction: column; align-items: flex-end; padding: 10px 14px; transform-origin: top right; }
        #rows { display: flex; flex-direction: column; align-items: flex-end; }
        body.left { align-items: flex-start; transform-origin: top left; }
        body.left #rows { align-items: flex-start; }
        .o { text-shadow: -3px -3px 0 #000, 3px -3px 0 #000, -3px 3px 0 #000, 3px 3px 0 #000,
                          0 -3px 0 #000, 0 3px 0 #000, -3px 0 0 #000, 3px 0 0 #000, 0 0 6px #000; }
        .total { font-size: 60px; line-height: 1.05; display: flex; align-items: center; gap: 12px; }
        .total.match { color: #ffcd46; }
        .row { font-size: 30px; line-height: 1.2; }
        .row .l { color: #ffe9a6; }
        .row.star, .row.star .l { color: #d6beff; }
        svg { width: 54px; height: 47px; filter: drop-shadow(0 0 2px #000); }
        .hidden { display: none; }
        </style></head>
        <body>
        <div id="total" class="total o hidden">
          <svg viewBox="0 0 64 56" aria-hidden="true">
            <defs>
              <clipPath id="c"><path d="M6 24V18Q6 6 32 6T58 18V24Z"/><rect x="6" y="24" width="52" height="28" rx="3"/></clipPath>
              <linearGradient id="lid" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#c07c3c"/><stop offset="1" stop-color="#8f5427"/></linearGradient>
              <linearGradient id="body" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#94582a"/><stop offset="1" stop-color="#6e3f1d"/></linearGradient>
              <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffe489"/><stop offset=".55" stop-color="#ffcd46"/><stop offset="1" stop-color="#d9961c"/></linearGradient>
              <linearGradient id="band" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f0b43a"/><stop offset="1" stop-color="#c47f12"/></linearGradient>
            </defs>
            <rect x="6" y="24" width="52" height="28" rx="3" fill="url(#body)"/>
            <path d="M6 24V18Q6 6 32 6T58 18V24Z" fill="url(#lid)"/>
            <g clip-path="url(#c)">
              <path d="M6 38H58" stroke="#5a3216" stroke-width="2"/>
              <path d="M6 15H58" stroke="#7a4520" stroke-width="1.5" opacity=".7"/>
              <rect x="12" y="0" width="8" height="56" fill="url(#band)"/>
              <rect x="44" y="0" width="8" height="56" fill="url(#band)"/>
            </g>
            <rect x="4" y="21" width="56" height="6" rx="1.5" fill="url(#gold)" stroke="#1c140c" stroke-width="2.5"/>
            <path d="M6 24V18Q6 6 32 6T58 18V24" fill="none" stroke="#1c140c" stroke-width="3" stroke-linejoin="round"/>
            <rect x="6" y="27" width="52" height="25" rx="3" fill="none" stroke="#1c140c" stroke-width="3"/>
            <rect x="26" y="22" width="12" height="14" rx="2" fill="url(#gold)" stroke="#1c140c" stroke-width="2.5"/>
            <circle cx="32" cy="28" r="2.2" fill="#1c140c"/><rect x="31" y="28" width="2" height="5" fill="#1c140c"/>
          </svg>
          <span id="value"></span>
        </div>
        <div id="rows"></div>
        <script>
        const q = new URLSearchParams(location.search);
        if (q.get('align') === 'left') document.body.classList.add('left');
        const scale = parseFloat(q.get('scale') || '1');
        if (scale > 0 && scale !== 1) document.body.style.transform = 'scale(' + scale + ')';
        const total = document.getElementById('total'), value = document.getElementById('value'), rows = document.getElementById('rows');
        let last = '';
        async function tick() {
          try {
            const r = await fetch('/overlay.json', { cache: 'no-store' });
            const text = await r.text();
            if (text === last) return;
            last = text;
            const d = JSON.parse(text);
            total.classList.remove('hidden');
            total.classList.toggle('match', !!d.inMatch);
            value.textContent = d.total;
            rows.replaceChildren(...d.rows.map(row => {
              const div = document.createElement('div');
              div.className = 'row o' + (row.star ? ' star' : '');
              const l = document.createElement('span');
              l.className = 'l';
              l.textContent = row.star ? '★ ' : row.label + ' • ';
              div.append(l, row.value);
              return div;
            }));
          } catch (e) {
            // chest-tracker closed or restarting: keep the last figures, try again.
          }
        }
        tick();
        setInterval(tick, 1000);
        </script>
        </body></html>
        """;
}
