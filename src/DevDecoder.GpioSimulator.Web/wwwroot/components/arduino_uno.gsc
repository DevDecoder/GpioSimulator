{
  "boardId": "arduino_uno",
  "displayName": "Arduino Uno R3",
  "layoutType": "split_headers",
  "visuals": {
    "boardColor": "#006699",
    "svgWidth": 600,
    "svgHeight": 400,
    "svgTemplate": "<svg viewBox=\"0 0 600 400\" width=\"100%\" height=\"100%\" xmlns=\"http://www.w3.org/2000/svg\">\n  <!-- Base Board -->\n  <rect x=\"10\" y=\"10\" width=\"580\" height=\"380\" rx=\"20\" ry=\"20\" fill=\"#006699\" stroke=\"#004b70\" stroke-width=\"4\" />\n  \n  <!-- Gold Silkscreen Border and Text -->\n  <rect x=\"20\" y=\"20\" width=\"560\" height=\"360\" rx=\"14\" ry=\"14\" fill=\"none\" stroke=\"#a1824a\" stroke-width=\"1.5\" stroke-dasharray=\"8, 4\" opacity=\"0.6\" />\n  <text x=\"300\" y=\"370\" fill=\"#a1824a\" font-size=\"14\" font-family=\"Courier, monospace\" font-weight=\"bold\" text-anchor=\"middle\" opacity=\"0.8\">ARDUINO UNO R3 - GEOMAPPED SIMULATOR</text>\n  \n  <!-- Main MCU (ATmega328P DIP) -->\n  <rect x=\"160\" y=\"210\" width=\"210\" height=\"40\" rx=\"3\" fill=\"#1e1e1e\" stroke=\"#333\" stroke-width=\"1.5\" />\n  <circle cx=\"170\" cy=\"230\" r=\"4\" fill=\"#111\" /> <!-- Chip Notch -->\n  <text x=\"265\" y=\"234\" fill=\"#fff\" font-size=\"12\" font-family=\"monospace\" font-weight=\"bold\" letter-spacing=\"2\" text-anchor=\"middle\">ATMEGA328P-PU</text>\n  <!-- Metal Pins of DIP Chip -->\n  <path d=\"M175,210 L175,205 M190,210 L190,205 M205,210 L205,205 M220,210 L220,205 M235,210 L235,205 M250,210 L250,205 M265,210 L265,205 M280,210 L280,205 M295,210 L295,205 M310,210 L310,205 M325,210 L325,205 M340,210 L340,205 M355,210 L355,205 M370,210 L370,205\" stroke=\"#bbb\" stroke-width=\"2\" />\n  <path d=\"M175,250 L175,255 M190,250 L190,255 M205,250 L205,255 M220,250 L220,255 M235,250 L235,255 M250,250 L250,255 M265,250 L265,255 M280,250 L280,255 M295,250 L295,255 M310,250 L310,255 M325,250 L325,255 M340,250 L340,255 M355,250 L355,255 M370,250 L370,255\" stroke=\"#bbb\" stroke-width=\"2\" />\n  \n  <!-- USB Type-B Port (Left Top) -->\n  <rect x=\"-10\" y=\"45\" width=\"90\" height=\"75\" rx=\"4\" fill=\"#d1d1d1\" stroke=\"#8f8f8f\" stroke-width=\"2\" />\n  <rect x=\"15\" y=\"60\" width=\"65\" height=\"45\" fill=\"#444\" rx=\"2\" />\n  \n  <!-- DC Power Barrel Jack (Left Bottom) -->\n  <rect x=\"-10\" y=\"240\" width=\"115\" height=\"85\" rx=\"5\" fill=\"#151515\" stroke=\"#333\" stroke-width=\"2\" />\n  <rect x=\"105\" y=\"260\" width=\"10\" height=\"45\" fill=\"#333\" />\n  \n  <!-- Red Reset Button (Left Top corner) -->\n  <rect x=\"520\" y=\"35\" width=\"35\" height=\"35\" rx=\"4\" fill=\"#222\" />\n  <circle cx=\"537\" cy=\"52\" r=\"10\" fill=\"#e53935\" stroke=\"#b71c1c\" stroke-width=\"1.5\" />\n  <text x=\"537\" y=\"83\" fill=\"#fff\" font-size=\"8\" font-family=\"sans-serif\" text-anchor=\"middle\" opacity=\"0.6\">RESET</text>\n  \n  <!-- Crystal Oscillator -->\n  <rect x=\"100\" y=\"145\" width=\"35\" height=\"18\" rx=\"8\" fill=\"#b5b5b5\" stroke=\"#777\" />\n  <text x=\"117\" y=\"157\" fill=\"#333\" font-size=\"8\" font-family=\"monospace\" text-anchor=\"middle\">16.0</text>\n  \n  <!-- Upper Header Blocks (Digital Pins 0-13, GND, AREF) -->\n  <!-- Digital Pins 8-13, GND, AREF Header -->\n  <rect x=\"175\" y=\"20\" width=\"185\" height=\"20\" rx=\"2\" fill=\"#1a1a1a\" />\n  <!-- Digital Pins 0-7 Header -->\n  <rect x=\"375\" y=\"20\" width=\"185\" height=\"20\" rx=\"2\" fill=\"#1a1a1a\" />\n  \n  <!-- Lower Header Blocks (Power, Analog In) -->\n  <!-- Power Header -->\n  <rect x=\"175\" y=\"355\" width=\"185\" height=\"20\" rx=\"2\" fill=\"#1a1a1a\" />\n  <!-- Analog In Header -->\n  <rect x=\"375\" y=\"355\" width=\"140\" height=\"20\" rx=\"2\" fill=\"#1a1a1a\" />\n  \n  <!-- Header Label Silkscreen Details -->\n  <text x=\"467\" y=\"53\" fill=\"#fff\" font-size=\"9\" font-family=\"sans-serif\" font-weight=\"bold\" text-anchor=\"middle\" opacity=\"0.8\">DIGITAL (PWM~)</text>\n  <text x=\"267\" y=\"345\" fill=\"#fff\" font-size=\"9\" font-family=\"sans-serif\" font-weight=\"bold\" text-anchor=\"middle\" opacity=\"0.8\">POWER</text>\n  <text x=\"445\" y=\"345\" fill=\"#fff\" font-size=\"9\" font-family=\"sans-serif\" font-weight=\"bold\" text-anchor=\"middle\" opacity=\"0.8\">ANALOG IN</text>\n</svg>"
  },
  "pins": [
    { "physical": 1, "logical": 0, "name": "D0 (RX)", "supportedModes": ["Input", "Output"], "x": 550, "y": 30 },
    { "physical": 2, "logical": 1, "name": "D1 (TX)", "supportedModes": ["Input", "Output"], "x": 528, "y": 30 },
    { "physical": 3, "logical": 2, "name": "D2", "supportedModes": ["Input", "Output"], "x": 506, "y": 30 },
    { "physical": 4, "logical": 3, "name": "D3 (~)", "supportedModes": ["Input", "Output"], "x": 484, "y": 30 },
    { "physical": 5, "logical": 4, "name": "D4", "supportedModes": ["Input", "Output"], "x": 462, "y": 30 },
    { "physical": 6, "logical": 5, "name": "D5 (~)", "supportedModes": ["Input", "Output"], "x": 440, "y": 30 },
    { "physical": 7, "logical": 6, "name": "D6 (~)", "supportedModes": ["Input", "Output"], "x": 418, "y": 30 },
    { "physical": 8, "logical": 7, "name": "D7", "supportedModes": ["Input", "Output"], "x": 396, "y": 30 },
    
    { "physical": 9, "logical": 8, "name": "D8", "supportedModes": ["Input", "Output"], "x": 350, "y": 30 },
    { "physical": 10, "logical": 9, "name": "D9 (~)", "supportedModes": ["Input", "Output"], "x": 328, "y": 30 },
    { "physical": 11, "logical": 10, "name": "D10 (~)", "supportedModes": ["Input", "Output"], "x": 306, "y": 30 },
    { "physical": 12, "logical": 11, "name": "D11 (~)", "supportedModes": ["Input", "Output"], "x": 284, "y": 30 },
    { "physical": 13, "logical": 12, "name": "D12", "supportedModes": ["Input", "Output"], "x": 262, "y": 30 },
    { "physical": 14, "logical": 13, "name": "D13 (LED)", "supportedModes": ["Input", "Output"], "x": 240, "y": 30 },
    { "physical": 15, "logical": null, "name": "GND", "supportedModes": [], "x": 218, "y": 30 },
    { "physical": 16, "logical": null, "name": "AREF", "supportedModes": [], "x": 196, "y": 30 },
    
    { "physical": 17, "logical": null, "name": "RESET", "supportedModes": [], "x": 185, "y": 365 },
    { "physical": 18, "logical": null, "name": "3.3V", "supportedModes": [], "x": 207, "y": 365 },
    { "physical": 19, "logical": null, "name": "5V", "supportedModes": [], "x": 229, "y": 365 },
    { "physical": 20, "logical": null, "name": "GND", "supportedModes": [], "x": 251, "y": 365 },
    { "physical": 21, "logical": null, "name": "GND", "supportedModes": [], "x": 273, "y": 365 },
    { "physical": 22, "logical": null, "name": "VIN", "supportedModes": [], "x": 295, "y": 365 },
    
    { "physical": 23, "logical": 14, "name": "Analog A0", "supportedModes": ["Input", "AnalogInput"], "x": 385, "y": 365 },
    { "physical": 24, "logical": 15, "name": "Analog A1", "supportedModes": ["Input", "AnalogInput"], "x": 407, "y": 365 },
    { "physical": 25, "logical": 16, "name": "Analog A2", "supportedModes": ["Input", "AnalogInput"], "x": 429, "y": 365 },
    { "physical": 26, "logical": 17, "name": "Analog A3", "supportedModes": ["Input", "AnalogInput"], "x": 451, "y": 365 },
    { "physical": 27, "logical": 18, "name": "Analog A4", "supportedModes": ["Input", "AnalogInput"], "x": 473, "y": 365 },
    { "physical": 28, "logical": 19, "name": "Analog A5", "supportedModes": ["Input", "AnalogInput"], "x": 495, "y": 365 }
  ]
}
