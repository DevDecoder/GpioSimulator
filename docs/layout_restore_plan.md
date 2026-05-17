# Implementation Plan: Golden Layout v2 Panel Restoration Fix

## 1. Executive Summary & Root Cause Analysis

### The Issue
When closing both the **Components** and **Activity Logs** panes, only the **Simulator Workspace (board)** remains. In Golden Layout v2, the layout engine automatically simplifies and collapses empty hierarchical layout groups (Rows/Columns) to keep the layout tree clean. 
As a result:
* The `myLayout.rootItem` collapses from a `Row` or `Column` into a raw **`ComponentItem`** (the workspace board itself).
* When a user tries to restore a closed pane (e.g., View -> Components Pane), our current implementation calls `myLayout.rootItem.addChild({...})`.
* Because `ComponentItem` is a leaf node and does not support child containers, the `.addChild()` method fails silently or throws a hierarchy error, preventing the panel from returning.

### The Solution
We will refactor the `togglePanel()` function in [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js) to:
1. Detect if the layout's root item has collapsed into a single component (`myLayout.rootItem.type === 'component'`).
2. Wrap the collapsed component inside a clean new layout group using Golden Layout v2's serialized state management:
   * **For Components**: Wrap the board in a horizontal **Row**, placing the restored components panel on the left (index `0`).
   * **For Logs**: Wrap the board in a vertical **Column**, placing the restored logs panel at the bottom.
3. If the layout is already a group, intelligently place the restored panel in its designated position (e.g. left of the root row for Components, or below the board in its column for Logs).

---

## 2. Proposed Changes

We propose the following clean drop-in replacement for the `togglePanel` function in [main.js:L483-506](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js#L483-506):

```javascript
function togglePanel(componentType, menuBtn) {
    const isChecked = menuBtn.classList.toggle('checked');
    if (isChecked) {
        // Re-append to Golden Layout
        if (myLayout.rootItem) {
            const newItemConfig = {
                type: 'component',
                componentType: componentType,
                title: componentType === 'components' ? 'Components' : 'Real-Time Activity Log',
                isClosable: true
            };
            
            if (componentType === 'components') {
                if (myLayout.rootItem.type === 'row') {
                    // Add components to the left of the root row
                    myLayout.rootItem.addChild(newItemConfig, 0);
                } else {
                    // Wrap current root in a row
                    const oldRootConfig = myLayout.saveLayout();
                    myLayout.loadLayout({
                        root: {
                            type: 'row',
                            content: [
                                newItemConfig,
                                oldRootConfig.root
                            ]
                        }
                    });
                }
            } else if (componentType === 'logs') {
                const boardItem = myLayout.rootItem.getComponentsByName('board')[0];
                if (boardItem && boardItem.parent && boardItem.parent.type === 'column') {
                    // Add logs below the board in its existing parent column
                    boardItem.parent.addChild(newItemConfig);
                } else {
                    // Wrap the board in a column with the logs below it
                    const oldRootConfig = myLayout.saveLayout();
                    
                    // Helper to replace the board component node inside the saved config with a column
                    function wrapBoardInConfig(node) {
                        if (!node) return null;
                        if (node.type === 'component' && node.componentType === 'board') {
                            return {
                                type: 'column',
                                content: [
                                    node,
                                    newItemConfig
                                ]
                            };
                        }
                        if (node.content) {
                            node.content = node.content.map(child => wrapBoardInConfig(child));
                        }
                        return node;
                    }
                    
                    const newRoot = wrapBoardInConfig(oldRootConfig.root);
                    myLayout.loadLayout({ root: newRoot });
                }
            }
        }
    } else {
        // Close from layout
        const targetItem = myLayout.rootItem.getComponentsByName(componentType)[0];
        if (targetItem) {
            if (typeof targetItem.remove === 'function') {
                targetItem.remove();
            } else if (targetItem.parent) {
                targetItem.parent.removeChild(targetItem);
            }
        }
    }
}
```

---

## 3. Verification Plan

After applying this fix, we will execute a browser subagent to:
1. Confirm the page loads correctly with the theme selector, shortened menus, and default `raspberry_pi_5_breakout` board active.
2. Toggle the `Components Pane` off under the `View` menu.
3. Toggle the `Activity Logs Pane` off under the `View` menu.
4. Verify the Workspace fills the entire page smoothly.
5. Toggle the `Components Pane` back **on** under the `View` menu.
6. Verify the components panel restores perfectly on the left.
7. Toggle the `Activity Logs Pane` back **on** under the `View` menu.
8. Verify the logs panel restores perfectly at the bottom.
9. Confirm no layout exceptions or console errors occur during these operations.
