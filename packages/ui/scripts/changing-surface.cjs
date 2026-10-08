const { app, BrowserWindow, screen } = require('electron');
app.whenReady().then(async () => {
  const bounds = screen.getPrimaryDisplay().bounds;
  const window = new BrowserWindow({ ...bounds, frame: false, autoHideMenuBar: true, alwaysOnTop: true,
    webPreferences: { backgroundThrottling: false, contextIsolation: true, sandbox: true, nodeIntegration: false } });
  const html = `<!doctype html><html><head><style>
  *{box-sizing:border-box}body{margin:0;background:#191d24;color:#e4e9f0;font:16px Segoe UI;overflow:hidden}
  header{height:68px;background:#242b35;padding:12px 22px}.small{font-size:12px;color:#abb9ca}
  main{display:flex;gap:20px;padding:24px}.side{width:18%}.body{flex:1}.detail{width:24%}
  .row{height:27px;margin:3px 0;padding:4px 10px;background:#242b35}.active{background:#527ab0}
  #progress{height:12px;width:10%;background:#527ab0;margin:20px 0}
  #marker{position:fixed;top:0;left:0;width:32px;height:32px;background:#ff00ff;z-index:9999}
  #exclusion-marker{position:fixed;left:50%;top:65%;width:100px;height:100px;background:#00ffff;z-index:9999}
  </style></head><body><div id="marker"></div><div id="exclusion-marker"></div><header>SYNTHETIC WORKSPACE · no personal content<div class="small">Short changing-content capture fixture</div></header>
  <main><div class="side">RECORDINGS<div id="sidebar"></div></div><div class="body">Activity / scrolling evidence<div id="rows"></div></div>
  <div class="detail">DETAILS<p id="selected">Selected item 001</p><p id="counter">Progress: 0%</p><div id="progress"></div><p>Generated test content only</p></div></main>
  <script>let n=0;const rows=document.getElementById('rows'),sidebar=document.getElementById('sidebar');
  rows.innerHTML=Array.from({length:30},(_,i)=>'<div class="row">'+String(i+1).padStart(3,'0')+'　 local-task-'+(i%12)+'　 /　 evidence item '+(i*7)+'</div>').join('');
  sidebar.innerHTML=Array.from({length:8},(_,i)=>'<div class="row">Example '+(i+1)+'</div>').join('');
  function advance(){n++;const i=n%30;rows.children[(i+29)%30].classList.remove('active');rows.children[i].classList.add('active');
  document.getElementById('selected').textContent='Selected item '+String(n+1).padStart(3,'0');
  document.getElementById('counter').textContent='Progress: '+(n%100)+'%';document.getElementById('progress').style.width=(n%100)+'%'}
  advance();setInterval(advance,100);</script></body></html>`;
  await window.loadURL(`data:text/html,${encodeURIComponent(html)}`);
  process.stdout.write('SURFACE_READY\n');
});
app.on('window-all-closed', () => app.quit());
