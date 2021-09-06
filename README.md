<p align="center">
    <h1>🔧 Udon Graph Tweaks 🔧</h1>
</p>
<hr>
<p align="center">
  <strong>Hotkeys, Shortcuts and other improvements to Udon Graph Workflow</strong>
</p>

<p align="center">
  <sub>Built with ❤︎ by
  <a href="https://twitter.com/orels1_">orels1</a> and
  <a href="https://github.com/orels1/UdonGraphTweaks/graphs/contributors">
    contributors
  </a>
  </sub>
</p>

## Quick Start Guide

### Requirements

- [VRC SDK3 with Udon](https://vrchat.com/home/download) (v 2021.08.11.15.16+)

### Installation

- [Download the repo](https://github.com/orels1/UdonGraphTweaks/archive/refs/heads/master.zip)
- Extract into the `Editor` folder inside your `Assets`
- Open the UGT window: Top Unity Menu -> Tools -> UdonGraphTweaks 
- Dock it somewhere, e.g. besides your Udon Graph window
- Enjoy!

## Usage

### Hotkeys

_Extra functionality when pressing keys while using the Udon Graph_

- Left-Click anywhere while holding a key:
  - 1: Create Float Const
  - 2: Create Vector2 Const (hold Shift for constructor)
  - 3: Create Vector3 Const (hold Shift for constructor)
  - 4: Create Vector4 Const (hold Shift for constructor)
  - 0: Create Int Const
  - S: Create String Const
  - Q: Create Bool Const
  - I: Create an Interact event
  - L: Create a Debug.Log node with an empty string const connected

- Left-Click on a Node while holding a key:
  - L: log the value of the node
  - B: break out x/y/z/w values of the Vector 2/3/4 outputs
  - C: Convert a Const node to a Variable (hold Shift to make it public)

- Ctrl+F: Open Graph Search
  - This will allow you to find and select all the nodes that match the search
  - Press Enter in the search field to jump between the results

### Shortcuts

_Shortcut buttons in the UGT window itself_

- Events: create the event nodes with null checking logic (where needed) and a Debug.Log to provide a quick setup
- Constants: create constant nodes of often used types. Same functionality as the Hotkey equivalent
- Extras:
  - Debug Log: if no node is selected - creates a new Debug.Log with a string const. If any node is selected - creates a Debug.Log connected to it
  - This GameObject: creates a `This` node
  - This Transform: creates a transform node that returns current GameObject's transform
  - Loop Over Array: creates a For loop that iterates over all the elements of the selected array node (can be a variable, const or anything else that returns an array)

### Options

- UGT Graph Theme: enables a custom Udon Graph theme that improves the canvas contrast and brings the UdonGraph window up to speed with Unity 2019 styling
