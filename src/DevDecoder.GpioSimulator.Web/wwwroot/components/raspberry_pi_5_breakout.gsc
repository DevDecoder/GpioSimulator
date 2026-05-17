{
  "boardId": "raspberry_pi_5_breakout",
  "displayName": "Raspberry Pi 5 (Breakout Breadboard)",
  "layoutType": "dual_row_header",
  "visuals": {
    "boardColor": "#0b1329",
    "svgWidth": 440,
    "svgHeight": 1320,
    "svgTemplate": "<svg viewBox=\"0 0 440 1320\" width=\"100%\" height=\"100%\" xmlns=\"http://www.w3.org/2000/svg\">\n  <!-- Base Ceramic Breadboard -->\n  <rect x=\"5\" y=\"5\" width=\"430\" height=\"1310\" rx=\"15\" fill=\"#fafaf9\" stroke=\"#cbd5e1\" stroke-width=\"3\" />\n  \n  <!-- Sockets/Holes Definitions -->\n  <defs>\n    <circle id=\"h\" cx=\"0\" cy=\"0\" r=\"3.5\" fill=\"#e2e8f0\" stroke=\"#cbd5e1\" stroke-width=\"0.8\" />\n    <circle id=\"ph\" cx=\"0\" cy=\"0\" r=\"3.5\" fill=\"#fee2e2\" stroke=\"#fca5a5\" stroke-width=\"0.8\" />\n    <circle id=\"nh\" cx=\"0\" cy=\"0\" r=\"3.5\" fill=\"#e0f2fe\" stroke=\"#93c5fd\" stroke-width=\"0.8\" />\n    \n    <!-- Top Row holes template (only a,b,c and h,i,j are visible) -->\n    <g id=\"r-top\">\n      <use href=\"#h\" x=\"100\" />\n      <use href=\"#h\" x=\"122\" />\n      <use href=\"#h\" x=\"144\" />\n      <use href=\"#h\" x=\"298\" />\n      <use href=\"#h\" x=\"320\" />\n      <use href=\"#h\" x=\"342\" />\n    </g>\n    \n    <!-- Bottom Row holes template (all a-j columns are visible) -->\n    <g id=\"r-full\">\n      <use href=\"#h\" x=\"100\" />\n      <use href=\"#h\" x=\"122\" />\n      <use href=\"#h\" x=\"144\" />\n      <use href=\"#h\" x=\"166\" />\n      <use href=\"#h\" x=\"188\" />\n      <use href=\"#h\" x=\"254\" />\n      <use href=\"#h\" x=\"276\" />\n      <use href=\"#h\" x=\"298\" />\n      <use href=\"#h\" x=\"320\" />\n      <use href=\"#h\" x=\"342\" />\n    </g>\n    \n    <!-- Power holes template -->\n    <g id=\"r-power\">\n      <use href=\"#nh\" x=\"38\" />\n      <use href=\"#ph\" x=\"60\" />\n      <use href=\"#ph\" x=\"382\" />\n      <use href=\"#nh\" x=\"404\" />\n    </g>\n  </defs>\n  \n  <!-- Center Divider Trough -->\n  <rect x=\"216\" y=\"40\" width=\"8\" height=\"1230\" fill=\"#e2e8f0\" />\n  \n  <!-- Left & Right Power Bus Vertical Lines -->\n  <line x1=\"38\" y1=\"50\" x2=\"38\" y2=\"1250\" stroke=\"#0284c7\" stroke-width=\"1.5\" />\n  <line x1=\"60\" y1=\"50\" x2=\"60\" y2=\"1250\" stroke=\"#ef4444\" stroke-width=\"1.5\" />\n  <line x1=\"382\" y1=\"50\" x2=\"382\" y2=\"1250\" stroke=\"#ef4444\" stroke-width=\"1.5\" />\n  <line x1=\"404\" y1=\"50\" x2=\"404\" y2=\"1250\" stroke=\"#0284c7\" stroke-width=\"1.5\" />\n  \n  <!-- Power Rail Symbols -->\n  <g fill=\"none\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">\n    <text x=\"38\" y=\"35\" fill=\"#0284c7\">-</text>\n    <text x=\"60\" y=\"35\" fill=\"#ef4444\">+</text>\n    <text x=\"382\" y=\"35\" fill=\"#ef4444\">+</text>\n    <text x=\"404\" y=\"35\" fill=\"#0284c7\">-</text>\n    \n    <text x=\"38\" y=\"1280\" fill=\"#0284c7\">-</text>\n    <text x=\"60\" y=\"1280\" fill=\"#ef4444\">+</text>\n    <text x=\"382\" y=\"1280\" fill=\"#ef4444\">+</text>\n    <text x=\"404\" y=\"1280\" fill=\"#0284c7\">-</text>\n  </g>\n  \n  <!-- Column Gutter Header/Footer Labels (a-e, f-j) -->\n  <g fill=\"#94a3b8\" font-family=\"Outfit, sans-serif\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"middle\">\n    <text x=\"100\" y=\"32\">a</text><text x=\"122\" y=\"32\">b</text><text x=\"144\" y=\"32\">c</text><text x=\"166\" y=\"32\">d</text><text x=\"188\" y=\"32\">e</text>\n    <text x=\"254\" y=\"32\">f</text><text x=\"276\" y=\"32\">g</text><text x=\"298\" y=\"32\">h</text><text x=\"320\" y=\"32\">i</text><text x=\"342\" y=\"32\">j</text>\n    \n    <text x=\"100\" y=\"1288\">a</text><text x=\"122\" y=\"1288\">b</text><text x=\"144\" y=\"1288\">c</text><text x=\"166\" y=\"1288\">d</text><text x=\"188\" y=\"1288\">e</text>\n    <text x=\"254\" y=\"1288\">f</text><text x=\"276\" y=\"1288\">g</text><text x=\"298\" y=\"1288\">h</text><text x=\"320\" y=\"1288\">i</text><text x=\"342\" y=\"1288\">j</text>\n  </g>\n  \n  <!-- Holes & Power Sockets Rendering -->\n  <g>\\n    <use href=\"#r-power\" y=\"60\" />\\n    <use href=\"#r-power\" y=\"80\" />\\n    <use href=\"#r-power\" y=\"100\" />\\n    <use href=\"#r-power\" y=\"120\" />\\n    <use href=\"#r-power\" y=\"140\" />\\n    <use href=\"#r-power\" y=\"160\" />\\n    <use href=\"#r-power\" y=\"180\" />\\n    <use href=\"#r-power\" y=\"200\" />\\n    <use href=\"#r-power\" y=\"220\" />\\n    <use href=\"#r-power\" y=\"240\" />\\n    <use href=\"#r-power\" y=\"260\" />\\n    <use href=\"#r-power\" y=\"280\" />\\n    <use href=\"#r-power\" y=\"300\" />\\n    <use href=\"#r-power\" y=\"320\" />\\n    <use href=\"#r-power\" y=\"340\" />\\n    <use href=\"#r-power\" y=\"360\" />\\n    <use href=\"#r-power\" y=\"380\" />\\n    <use href=\"#r-power\" y=\"400\" />\\n    <use href=\"#r-power\" y=\"420\" />\\n    <use href=\"#r-power\" y=\"440\" />\\n    <use href=\"#r-power\" y=\"460\" />\\n    <use href=\"#r-power\" y=\"480\" />\\n    <use href=\"#r-power\" y=\"500\" />\\n    <use href=\"#r-power\" y=\"520\" />\\n    <use href=\"#r-power\" y=\"540\" />\\n    <use href=\"#r-power\" y=\"560\" />\\n    <use href=\"#r-power\" y=\"580\" />\\n    <use href=\"#r-power\" y=\"600\" />\\n    <use href=\"#r-power\" y=\"620\" />\\n    <use href=\"#r-power\" y=\"640\" />\\n    <use href=\"#r-power\" y=\"660\" />\\n    <use href=\"#r-power\" y=\"680\" />\\n    <use href=\"#r-power\" y=\"700\" />\\n    <use href=\"#r-power\" y=\"720\" />\\n    <use href=\"#r-power\" y=\"740\" />\\n    <use href=\"#r-power\" y=\"760\" />\\n    <use href=\"#r-power\" y=\"780\" />\\n    <use href=\"#r-power\" y=\"800\" />\\n    <use href=\"#r-power\" y=\"820\" />\\n    <use href=\"#r-power\" y=\"840\" />\\n    <use href=\"#r-power\" y=\"860\" />\\n    <use href=\"#r-power\" y=\"880\" />\\n    <use href=\"#r-power\" y=\"900\" />\\n    <use href=\"#r-power\" y=\"920\" />\\n    <use href=\"#r-power\" y=\"940\" />\\n    <use href=\"#r-power\" y=\"960\" />\\n    <use href=\"#r-power\" y=\"980\" />\\n    <use href=\"#r-power\" y=\"1000\" />\\n    <use href=\"#r-power\" y=\"1020\" />\\n    <use href=\"#r-power\" y=\"1040\" />\\n    <use href=\"#r-power\" y=\"1060\" />\\n    <use href=\"#r-power\" y=\"1080\" />\\n    <use href=\"#r-power\" y=\"1100\" />\\n    <use href=\"#r-power\" y=\"1120\" />\\n    <use href=\"#r-power\" y=\"1140\" />\\n    <use href=\"#r-power\" y=\"1160\" />\\n    <use href=\"#r-power\" y=\"1180\" />\\n    <use href=\"#r-power\" y=\"1200\" />\\n    <use href=\"#r-power\" y=\"1220\" />\\n    <use href=\"#r-power\" y=\"1240\" />\\n    <use href=\"#r-top\" y=\"60\" />\\n    <use href=\"#r-top\" y=\"80\" />\\n    <use href=\"#r-top\" y=\"100\" />\\n    <use href=\"#r-top\" y=\"120\" />\\n    <use href=\"#r-top\" y=\"140\" />\\n    <use href=\"#r-top\" y=\"160\" />\\n    <use href=\"#r-top\" y=\"180\" />\\n    <use href=\"#r-top\" y=\"200\" />\\n    <use href=\"#r-top\" y=\"220\" />\\n    <use href=\"#r-top\" y=\"240\" />\\n    <use href=\"#r-top\" y=\"260\" />\\n    <use href=\"#r-top\" y=\"280\" />\\n    <use href=\"#r-top\" y=\"300\" />\\n    <use href=\"#r-top\" y=\"320\" />\\n    <use href=\"#r-top\" y=\"340\" />\\n    <use href=\"#r-top\" y=\"360\" />\\n    <use href=\"#r-top\" y=\"380\" />\\n    <use href=\"#r-top\" y=\"400\" />\\n    <use href=\"#r-top\" y=\"420\" />\\n    <use href=\"#r-top\" y=\"440\" />\\n    <use href=\"#r-full\" y=\"460\" />\\n    <use href=\"#r-full\" y=\"480\" />\\n    <use href=\"#r-full\" y=\"500\" />\\n    <use href=\"#r-full\" y=\"520\" />\\n    <use href=\"#r-full\" y=\"540\" />\\n    <use href=\"#r-full\" y=\"560\" />\\n    <use href=\"#r-full\" y=\"580\" />\\n    <use href=\"#r-full\" y=\"600\" />\\n    <use href=\"#r-full\" y=\"620\" />\\n    <use href=\"#r-full\" y=\"640\" />\\n    <use href=\"#r-full\" y=\"660\" />\\n    <use href=\"#r-full\" y=\"680\" />\\n    <use href=\"#r-full\" y=\"700\" />\\n    <use href=\"#r-full\" y=\"720\" />\\n    <use href=\"#r-full\" y=\"740\" />\\n    <use href=\"#r-full\" y=\"760\" />\\n    <use href=\"#r-full\" y=\"780\" />\\n    <use href=\"#r-full\" y=\"800\" />\\n    <use href=\"#r-full\" y=\"820\" />\\n    <use href=\"#r-full\" y=\"840\" />\\n    <use href=\"#r-full\" y=\"860\" />\\n    <use href=\"#r-full\" y=\"880\" />\\n    <use href=\"#r-full\" y=\"900\" />\\n    <use href=\"#r-full\" y=\"920\" />\\n    <use href=\"#r-full\" y=\"940\" />\\n    <use href=\"#r-full\" y=\"960\" />\\n    <use href=\"#r-full\" y=\"980\" />\\n    <use href=\"#r-full\" y=\"1000\" />\\n    <use href=\"#r-full\" y=\"1020\" />\\n    <use href=\"#r-full\" y=\"1040\" />\\n    <use href=\"#r-full\" y=\"1060\" />\\n    <use href=\"#r-full\" y=\"1080\" />\\n    <use href=\"#r-full\" y=\"1100\" />\\n    <use href=\"#r-full\" y=\"1120\" />\\n    <use href=\"#r-full\" y=\"1140\" />\\n    <use href=\"#r-full\" y=\"1160\" />\\n    <use href=\"#r-full\" y=\"1180\" />\\n    <use href=\"#r-full\" y=\"1200\" />\\n    <use href=\"#r-full\" y=\"1220\" />\\n    <use href=\"#r-full\" y=\"1240\" />\\n  </g>\\n  <!-- Row Numbers -->\\n  <g fill=\"#94a3b8\" font-family=\"Outfit, sans-serif\" font-size=\"10\" font-weight=\"bold\" text-anchor=\"middle\" dominant-baseline=\"middle\">\\n    <text x=\"150\" y=\"60\">1</text>\\n    <text x=\"292\" y=\"60\">1</text>\\n    <text x=\"150\" y=\"140\">5</text>\\n    <text x=\"292\" y=\"140\">5</text>\\n    <text x=\"150\" y=\"240\">10</text>\\n    <text x=\"292\" y=\"240\">10</text>\\n    <text x=\"150\" y=\"340\">15</text>\\n    <text x=\"292\" y=\"340\">15</text>\\n    <text x=\"150\" y=\"440\">20</text>\\n    <text x=\"292\" y=\"440\">20</text>\\n    <text x=\"150\" y=\"540\">25</text>\\n    <text x=\"292\" y=\"540\">25</text>\\n    <text x=\"150\" y=\"640\">30</text>\\n    <text x=\"292\" y=\"640\">30</text>\\n    <text x=\"150\" y=\"740\">35</text>\\n    <text x=\"292\" y=\"740\">35</text>\\n    <text x=\"150\" y=\"840\">40</text>\\n    <text x=\"292\" y=\"840\">40</text>\\n    <text x=\"150\" y=\"940\">45</text>\\n    <text x=\"292\" y=\"940\">45</text>\\n    <text x=\"150\" y=\"1040\">50</text>\\n    <text x=\"292\" y=\"1040\">50</text>\\n    <text x=\"150\" y=\"1140\">55</text>\\n    <text x=\"292\" y=\"1140\">55</text>\\n    <text x=\"150\" y=\"1240\">60</text>\\n    <text x=\"292\" y=\"1240\">60</text>\\n  </g>\\n  \n  <!-- NAVY BLUE BREAKOUT BOARD OVERLAY (Rows 1-20) -->\n  <rect x=\"158\" y=\"45\" width=\"124\" height=\"410\" rx=\"8\" fill=\"#0b1329\" stroke=\"#1e293b\" stroke-width=\"2\" />\n  <rect x=\"163\" y=\"50\" width=\"114\" height=\"400\" rx=\"6\" fill=\"none\" stroke=\"#c5a059\" stroke-dasharray=\"6, 4\" stroke-width=\"1.2\" opacity=\"0.8\" />\n  \n  <!-- Board Silkscreen Text labels inside the Navy board -->\n  <g fill=\"#ffffff\" font-family=\"Outfit, sans-serif\" font-size=\"9\" font-weight=\"bold\" dominant-baseline=\"middle\">\\n    <text x=\"215\" y=\"60\" text-anchor=\"end\">3.3V</text>\\n    <text x=\"227\" y=\"60\" text-anchor=\"start\">5V</text>\\n    <text x=\"215\" y=\"80\" text-anchor=\"end\">SDA1</text>\\n    <text x=\"227\" y=\"80\" text-anchor=\"start\">5V</text>\\n    <text x=\"215\" y=\"100\" text-anchor=\"end\">SCL1</text>\\n    <text x=\"227\" y=\"100\" text-anchor=\"start\">GND</text>\\n    <text x=\"215\" y=\"120\" text-anchor=\"end\">GPIO4</text>\\n    <text x=\"227\" y=\"120\" text-anchor=\"start\">TXD0</text>\\n    <text x=\"215\" y=\"140\" text-anchor=\"end\">GND</text>\\n    <text x=\"227\" y=\"140\" text-anchor=\"start\">RXD0</text>\\n    <text x=\"215\" y=\"160\" text-anchor=\"end\">GPIO17</text>\\n    <text x=\"227\" y=\"160\" text-anchor=\"start\">GPIO18</text>\\n    <text x=\"215\" y=\"180\" text-anchor=\"end\">GPIO27</text>\\n    <text x=\"227\" y=\"180\" text-anchor=\"start\">GND</text>\\n    <text x=\"215\" y=\"200\" text-anchor=\"end\">GPIO22</text>\\n    <text x=\"227\" y=\"200\" text-anchor=\"start\">GPIO23</text>\\n    <text x=\"215\" y=\"220\" text-anchor=\"end\">3.3V</text>\\n    <text x=\"227\" y=\"220\" text-anchor=\"start\">GPIO24</text>\\n    <text x=\"215\" y=\"240\" text-anchor=\"end\">MOSI</text>\\n    <text x=\"227\" y=\"240\" text-anchor=\"start\">GND</text>\\n    <text x=\"215\" y=\"260\" text-anchor=\"end\">MISO</text>\\n    <text x=\"227\" y=\"260\" text-anchor=\"start\">GPIO25</text>\\n    <text x=\"215\" y=\"280\" text-anchor=\"end\">SCLK</text>\\n    <text x=\"227\" y=\"280\" text-anchor=\"start\">CE0</text>\\n    <text x=\"215\" y=\"300\" text-anchor=\"end\">GND</text>\\n    <text x=\"227\" y=\"300\" text-anchor=\"start\">CE1</text>\\n    <text x=\"215\" y=\"320\" text-anchor=\"end\">SDA0</text>\\n    <text x=\"227\" y=\"320\" text-anchor=\"start\">SCL0</text>\\n    <text x=\"215\" y=\"340\" text-anchor=\"end\">GPIO5</text>\\n    <text x=\"227\" y=\"340\" text-anchor=\"start\">GND</text>\\n    <text x=\"215\" y=\"360\" text-anchor=\"end\">GPIO6</text>\\n    <text x=\"227\" y=\"360\" text-anchor=\"start\">GPIO12</text>\\n    <text x=\"215\" y=\"380\" text-anchor=\"end\">GPIO13</text>\\n    <text x=\"227\" y=\"380\" text-anchor=\"start\">GND</text>\\n    <text x=\"215\" y=\"400\" text-anchor=\"end\">GPIO19</text>\\n    <text x=\"227\" y=\"400\" text-anchor=\"start\">GPIO16</text>\\n    <text x=\"215\" y=\"420\" text-anchor=\"end\">GPIO26</text>\\n    <text x=\"227\" y=\"420\" text-anchor=\"start\">GPIO20</text>\\n    <text x=\"215\" y=\"440\" text-anchor=\"end\">GND</text>\\n    <text x=\"227\" y=\"440\" text-anchor=\"start\">GPIO21</text>\\n  </g>\n  \n  <!-- Golden Sockets for the 40 Pin headers -->\n  <g>\\n    <circle cx=\"188\" cy=\"60\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"60\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"60\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"60\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"80\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"80\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"80\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"80\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"100\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"100\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"100\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"100\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"120\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"120\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"120\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"120\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"140\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"140\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"140\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"140\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"160\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"160\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"160\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"160\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"180\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"180\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"180\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"180\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"200\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"200\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"200\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"200\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"220\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"220\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"220\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"220\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"240\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"240\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"240\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"240\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"260\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"260\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"260\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"260\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"280\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"280\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"280\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"280\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"300\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"300\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"300\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"300\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"320\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"320\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"320\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"320\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"340\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"340\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"340\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"340\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"360\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"360\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"360\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"360\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"380\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"380\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"380\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"380\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"400\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"400\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"400\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"400\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"420\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"420\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"420\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"420\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"188\" cy=\"440\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"188\" cy=\"440\" r=\"2\" fill=\"#0f172a\" />\\n    <circle cx=\"254\" cy=\"440\" r=\"4.5\" fill=\"#1e293b\" stroke=\"#c5a059\" stroke-width=\"1.2\" />\\n    <circle cx=\"254\" cy=\"440\" r=\"2\" fill=\"#0f172a\" />\\n  </g>\n  \n  <!-- Gold Header Title Ribbon -->\n  <text x=\"220\" y=\"1275\" fill=\"#94a3b8\" font-family=\"Outfit, sans-serif\" font-size=\"12\" font-weight=\"bold\" letter-spacing=\"1.5\" text-anchor=\"middle\">GPIO SIMULATOR BREADBOARD</text>\n</svg>"
  },
  "pins": [
    {
      "physical": 1,
      "logical": null,
      "name": "3.3V Power",
      "supportedModes": [],
      "x": 188,
      "y": 60
    },
    {
      "physical": 2,
      "logical": null,
      "name": "5V Power",
      "supportedModes": [],
      "x": 254,
      "y": 60
    },
    {
      "physical": 3,
      "logical": 2,
      "name": "GPIO 2 (SDA)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 80
    },
    {
      "physical": 4,
      "logical": null,
      "name": "5V Power",
      "supportedModes": [],
      "x": 254,
      "y": 80
    },
    {
      "physical": 5,
      "logical": 3,
      "name": "GPIO 3 (SCL)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 100
    },
    {
      "physical": 6,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 254,
      "y": 100
    },
    {
      "physical": 7,
      "logical": 4,
      "name": "GPIO 4",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 120
    },
    {
      "physical": 8,
      "logical": 14,
      "name": "GPIO 14 (TX)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 120
    },
    {
      "physical": 9,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 188,
      "y": 140
    },
    {
      "physical": 10,
      "logical": 15,
      "name": "GPIO 15 (RX)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 140
    },
    {
      "physical": 11,
      "logical": 17,
      "name": "GPIO 17",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 160
    },
    {
      "physical": 12,
      "logical": 18,
      "name": "GPIO 18",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 160
    },
    {
      "physical": 13,
      "logical": 27,
      "name": "GPIO 27",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 180
    },
    {
      "physical": 14,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 254,
      "y": 180
    },
    {
      "physical": 15,
      "logical": 22,
      "name": "GPIO 22",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 200
    },
    {
      "physical": 16,
      "logical": 23,
      "name": "GPIO 23",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 200
    },
    {
      "physical": 17,
      "logical": null,
      "name": "3.3V Power",
      "supportedModes": [],
      "x": 188,
      "y": 220
    },
    {
      "physical": 18,
      "logical": 24,
      "name": "GPIO 24",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 220
    },
    {
      "physical": 19,
      "logical": 10,
      "name": "GPIO 10 (MOSI)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 240
    },
    {
      "physical": 20,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 254,
      "y": 240
    },
    {
      "physical": 21,
      "logical": 9,
      "name": "GPIO 9 (MISO)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 260
    },
    {
      "physical": 22,
      "logical": 25,
      "name": "GPIO 25",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 260
    },
    {
      "physical": 23,
      "logical": 11,
      "name": "GPIO 11 (SCLK)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 280
    },
    {
      "physical": 24,
      "logical": 8,
      "name": "GPIO 8 (CE0)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 280
    },
    {
      "physical": 25,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 188,
      "y": 300
    },
    {
      "physical": 26,
      "logical": 7,
      "name": "GPIO 7 (CE1)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 300
    },
    {
      "physical": 27,
      "logical": 0,
      "name": "GPIO 0 (ID_SD)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 320
    },
    {
      "physical": 28,
      "logical": 1,
      "name": "GPIO 1 (ID_SC)",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 320
    },
    {
      "physical": 29,
      "logical": 5,
      "name": "GPIO 5",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 340
    },
    {
      "physical": 30,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 254,
      "y": 340
    },
    {
      "physical": 31,
      "logical": 6,
      "name": "GPIO 6",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 360
    },
    {
      "physical": 32,
      "logical": 12,
      "name": "GPIO 12",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 360
    },
    {
      "physical": 33,
      "logical": 13,
      "name": "GPIO 13",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 380
    },
    {
      "physical": 34,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 254,
      "y": 380
    },
    {
      "physical": 35,
      "logical": 19,
      "name": "GPIO 19",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 400
    },
    {
      "physical": 36,
      "logical": 16,
      "name": "GPIO 16",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 400
    },
    {
      "physical": 37,
      "logical": 26,
      "name": "GPIO 26",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 188,
      "y": 420
    },
    {
      "physical": 38,
      "logical": 20,
      "name": "GPIO 20",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 420
    },
    {
      "physical": 39,
      "logical": null,
      "name": "GND",
      "supportedModes": [],
      "x": 188,
      "y": 440
    },
    {
      "physical": 40,
      "logical": 21,
      "name": "GPIO 21",
      "supportedModes": [
        "Input",
        "Output"
      ],
      "x": 254,
      "y": 440
    }
  ]
}