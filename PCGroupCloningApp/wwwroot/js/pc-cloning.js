// pc-cloning.js - PC Group Cloning functionality

// Global variables
let selectedSourceComputer = '';
let selectedTargetComputer = '';
let sourceComputerOU = '';
let sourceGroups = [];
let additionalGroups = [];
let searchTimeouts = {};

// Initialize when page loads
document.addEventListener('DOMContentLoaded', function () {
    console.log('PC Cloning script loaded');
    initializeEventListeners();
});

function initializeEventListeners() {
    // Computer search event listeners
    setupComputerSearch('sourceComputerSearch', 'sourceComputerDropdown', selectSourceComputer);
    setupComputerSearch('targetComputerSearch', 'targetComputerDropdown', selectTargetComputer);

    // Group search event listeners
    setupGroupSearch();

    // Button event listeners
    document.getElementById('selectAllGroups')?.addEventListener('click', selectAllGroups);
    document.getElementById('deselectAllGroups')?.addEventListener('click', deselectAllGroups);
    document.getElementById('executeClone')?.addEventListener('click', executeClone);

    // Hide dropdowns when clicking outside
    document.addEventListener('click', hideDropdownsOnClickOutside);
}

// Computer search setup
function setupComputerSearch(inputId, dropdownId, selectCallback) {
    const input = document.getElementById(inputId);
    const dropdown = document.getElementById(dropdownId);

    if (!input || !dropdown) return;

    input.addEventListener('input', function (e) {
        const term = e.target.value;
        clearTimeout(searchTimeouts[inputId]);

        if (term.length < 2) {
            hideDropdown(dropdownId);
            return;
        }

        searchTimeouts[inputId] = setTimeout(async () => {
            await searchComputers(term, dropdownId, selectCallback);
        }, 300);
    });

    setupKeyboardNavigation(input, dropdown, selectCallback);
}

// Computer search function
async function searchComputers(term, dropdownId, selectCallback) {
    try {
        const response = await fetch(`/api/computer/search?term=${encodeURIComponent(term)}`);
        const computers = await response.json();
        displayComputerDropdown(computers, dropdownId, selectCallback);
    } catch (error) {
        console.error('Error searching computers:', error);
    }
}

// Display computer search results
function displayComputerDropdown(computers, dropdownId, selectCallback) {
    const dropdown = document.getElementById(dropdownId);

    if (computers.length === 0) {
        dropdown.innerHTML = '<div class="dropdown-item-text">No computers found</div>';
    } else {
        dropdown.innerHTML = computers.map(computer =>
            `<button type="button" class="dropdown-item" onclick="${selectCallback.name}('${computer}')">${computer}</button>`
        ).join('');
    }

    dropdown.style.display = 'block';
}

// Select source computer
async function selectSourceComputer(computerName) {
    selectedSourceComputer = computerName;

    document.getElementById('sourceComputerSearch').value = computerName;
    document.getElementById('selectedSourceComputer').textContent = computerName;
    hideDropdown('sourceComputerDropdown');

    // Get computer OU and groups
    await loadSourceComputerDetails(computerName);

    updatePreview();
    updateExecuteButton();
}

// Select target computer
function selectTargetComputer(computerName) {
    selectedTargetComputer = computerName;

    document.getElementById('targetComputerSearch').value = computerName;
    document.getElementById('selectedTargetComputer').textContent = computerName;
    hideDropdown('targetComputerDropdown');

    // Show target computer info
    document.getElementById('targetComputerInfo').style.display = 'block';

    updatePreview();
    updateExecuteButton();
}

// Load source computer details (OU and groups)
async function loadSourceComputerDetails(computerName) {
    try {
        // Get computer OU
        const ouResponse = await fetch(`/api/computer/${encodeURIComponent(computerName)}/ou`);
        const ouData = await ouResponse.json();
        sourceComputerOU = ouData.ou || '';

        document.getElementById('sourceComputerOU').textContent = sourceComputerOU || 'Unknown';
        document.getElementById('sourceComputerInfo').style.display = 'block';

        // Get computer groups
        const groupsResponse = await fetch(`/api/computer/${encodeURIComponent(computerName)}/groups`);
        sourceGroups = await groupsResponse.json();

        displaySourceGroups(sourceGroups);

    } catch (error) {
        console.error('Error loading source computer details:', error);
    }
}

// Display source groups with checkboxes
function displaySourceGroups(groups) {
    const container = document.getElementById('sourceGroupsList');
    document.getElementById('groupCount').textContent = groups.length;

    if (groups.length === 0) {
        container.innerHTML = '<div class="col-12"><div class="text-muted text-center py-2">No groups found</div></div>';
        return;
    }

    const groupsHtml = groups.map(group => `
        <div class="col-12">
            <div class="form-check">
                <input class="form-check-input" type="checkbox" id="group_${group}" value="${group}" onchange="updatePreview()" checked>
                <label class="form-check-label" for="group_${group}">
                    ${group}
                </label>
            </div>
        </div>
    `).join('');

    container.innerHTML = groupsHtml;

    // Show the groups card
    document.getElementById('sourceGroupsCard').style.display = 'block';
}

// Group search setup
function setupGroupSearch() {
    const input = document.getElementById('additionalGroupSearch');
    const dropdown = document.getElementById('additionalGroupDropdown');

    if (!input || !dropdown) return;

    input.addEventListener('input', function (e) {
        const term = e.target.value;
        clearTimeout(searchTimeouts['groupSearch']);

        if (term.length < 2) {
            hideDropdown('additionalGroupDropdown');
            return;
        }

        searchTimeouts['groupSearch'] = setTimeout(async () => {
            await searchGroups(term);
        }, 300);
    });

    setupKeyboardNavigation(input, dropdown, selectAdditionalGroup);
}

// Search groups function
async function searchGroups(term) {
    try {
        const response = await fetch(`/api/groups/search?term=${encodeURIComponent(term)}`);
        const groups = await response.json();
        displayGroupDropdown(groups);
    } catch (error) {
        console.error('Error searching groups:', error);
    }
}

// Display group search results
function displayGroupDropdown(groups) {
    const dropdown = document.getElementById('additionalGroupDropdown');

    if (groups.length === 0) {
        dropdown.innerHTML = '<div class="dropdown-item-text">No groups found</div>';
    } else {
        dropdown.innerHTML = groups.map(group =>
            `<button type="button" class="dropdown-item" onclick="selectAdditionalGroup('${group.replace(/'/g, "\\'")}')">${group}</button>`
        ).join('');
    }

    dropdown.style.display = 'block';
}

// Select additional group
function selectAdditionalGroup(groupName) {
    if (!additionalGroups.includes(groupName)) {
        additionalGroups.push(groupName);
        displayAdditionalGroups();
        updatePreview();
    }

    document.getElementById('additionalGroupSearch').value = '';
    hideDropdown('additionalGroupDropdown');
}

// Display selected additional groups
function displayAdditionalGroups() {
    const container = document.getElementById('additionalGroupsList');

    if (additionalGroups.length === 0) {
        container.innerHTML = '<small class="text-muted">No additional groups selected</small>';
        return;
    }

    const groupsHtml = additionalGroups.map(group => `
        <span class="badge bg-secondary me-1 mb-1">
            ${group}
            <button type="button" class="btn-close btn-close-white ms-1" style="font-size: 0.6em;" onclick="removeAdditionalGroup('${group.replace(/'/g, "\\'")}')"></button>
        </span>
    `).join('');

    container.innerHTML = groupsHtml;
}

// Remove additional group
function removeAdditionalGroup(groupName) {
    additionalGroups = additionalGroups.filter(g => g !== groupName);
    displayAdditionalGroups();
    updatePreview();
}

// Select/Deselect all groups
function selectAllGroups() {
    const checkboxes = document.querySelectorAll('#sourceGroupsList input[type="checkbox"]');
    checkboxes.forEach(cb => cb.checked = true);
    updatePreview();
}

function deselectAllGroups() {
    const checkboxes = document.querySelectorAll('#sourceGroupsList input[type="checkbox"]');
    checkboxes.forEach(cb => cb.checked = false);
    updatePreview();
}

// Update preview section
function updatePreview() {
    document.getElementById('previewSource').textContent = selectedSourceComputer || 'Not selected';
    document.getElementById('previewTarget').textContent = selectedTargetComputer || 'Not selected';

    const selectedGroups = getSelectedGroups();
    document.getElementById('previewGroupCount').textContent = selectedGroups.length;
    document.getElementById('previewAdditionalCount').textContent = additionalGroups.length;
}

// Get selected groups from checkboxes
function getSelectedGroups() {
    const checkboxes = document.querySelectorAll('#sourceGroupsList input[type="checkbox"]:checked');
    return Array.from(checkboxes).map(cb => cb.value);
}

// Update execute button state
function updateExecuteButton() {
    const button = document.getElementById('executeClone');
    const canExecute = selectedSourceComputer && selectedTargetComputer;

    button.disabled = !canExecute;

    if (canExecute) {
        button.classList.remove('btn-secondary');
        button.classList.add('btn-success');
    } else {
        button.classList.remove('btn-success');
        button.classList.add('btn-secondary');
    }
}

// Execute clone operation
async function executeClone() {
    const selectedGroups = getSelectedGroups();
    const keepSourceInPlace = document.getElementById('keepSourceInPlace').checked; // NEW: Get checkbox value

    if (!selectedSourceComputer || !selectedTargetComputer) {
        showAlert('Please select both source and target computers.', 'danger');
        return;
    }

    if (selectedGroups.length === 0 && additionalGroups.length === 0) {
        showAlert('Please select at least one group to clone.', 'warning');
        return;
    }

    // Show loading state
    const button = document.getElementById('executeClone');
    const originalText = button.innerHTML;
    button.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';
    button.disabled = true;

    try {
        const response = await fetch('/api/clone/execute', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                sourceComputer: selectedSourceComputer,
                targetComputer: selectedTargetComputer,
                selectedGroups: selectedGroups,
                additionalGroups: additionalGroups,
                sourceComputerOU: sourceComputerOU,
                keepSourceInPlace: keepSourceInPlace // NEW: Send checkbox value instead of moveToSameOU
            })
        });

        const result = await response.json();

        if (response.ok) {
            displayResults(result);
        } else {
            showAlert(`Error: ${result.message || 'Clone operation failed'}`, 'danger');
        }

    } catch (error) {
        console.error('Clone operation error:', error);
        showAlert('Network error occurred during clone operation.', 'danger');
    } finally {
        // Restore button
        button.innerHTML = originalText;
        button.disabled = false;
    }
}
// Reset page for new clone operation
function resetPage() {
    // Clear all selections
    selectedSourceComputer = '';
    selectedTargetComputer = '';
    sourceComputerOU = '';
    sourceGroups = [];
    additionalGroups = [];

    // Clear input fields
    document.getElementById('sourceComputerSearch').value = '';
    document.getElementById('targetComputerSearch').value = '';
    document.getElementById('additionalGroupSearch').value = '';

    // Hide info sections
    document.getElementById('sourceComputerInfo').style.display = 'none';
    document.getElementById('targetComputerInfo').style.display = 'none';
    document.getElementById('resultsCard').style.display = 'none';

    // Reset groups display
    document.getElementById('sourceGroupsList').innerHTML = '<div class="col-12"><div class="text-muted text-center py-4"><i class="fas fa-arrow-up"></i><br>Select a source computer to see its groups</div></div>';
    document.getElementById('additionalGroupsList').innerHTML = '<small class="text-muted">No additional groups selected</small>';

    // Reset checkbox
    document.getElementById('keepSourceInPlace').checked = false;

    // Update preview and button
    updatePreview();
    updateExecuteButton();

    // Scroll to top
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// Display operation results
function displayResults(result) {
    const card = document.getElementById('resultsCard');
    const content = document.getElementById('resultsContent');

    let html = `
        <div class="alert ${result.success ? 'alert-success' : 'alert-warning'}" role="alert">
            <h6><i class="fas ${result.success ? 'fa-check-circle' : 'fa-exclamation-triangle'}"></i> ${result.message}</h6>
        </div>
    `;

    if (result.operations && result.operations.length > 0) {
        html += `
            <h6>Operations Performed:</h6>
            <ul class="list-group list-group-flush mb-3">
                ${result.operations.map(op => `<li class="list-group-item"><i class="fas fa-check text-success"></i> ${op}</li>`).join('')}
            </ul>
        `;
    }

    if (result.errors && result.errors.length > 0) {
        html += `
            <h6 class="text-danger">Errors:</h6>
            <ul class="list-group list-group-flush mb-3">
                ${result.errors.map(error => `<li class="list-group-item list-group-item-danger"><i class="fas fa-times"></i> ${error}</li>`).join('')}
            </ul>
        `;
    }

    if (result.successCount !== undefined) {
        html += `
            <div class="row">
                <div class="col-md-6">
                    <div class="text-center">
                        <h4 class="text-success">${result.successCount}</h4>
                        <small class="text-muted">Successful Operations</small>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="text-center">
                        <h4 class="${result.errorCount > 0 ? 'text-danger' : 'text-muted'}">${result.errorCount || 0}</h4>
                        <small class="text-muted">Errors</small>
                    </div>
                </div>
            </div>
        `;
    }
    // Add reset button at the end
    html += `
    <div class="d-grid mt-4">
        <button type="button" class="btn btn-primary btn-lg" onclick="resetPage()">
            <i class="fas fa-redo"></i> Start New Clone Operation
        </button>
    </div>
`;

    content.innerHTML = html;
    card.style.display = 'block';

    // Scroll to results
    card.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// Show alert message
function showAlert(message, type) {
    // You can implement a toast notification system here
    alert(message); // Simple fallback
}

// Keyboard navigation setup
function setupKeyboardNavigation(input, dropdown, selectCallback) {
    input.addEventListener('keydown', function (e) {
        const items = dropdown.querySelectorAll('.dropdown-item');
        if (items.length === 0) return;

        let selectedIndex = Array.from(items).findIndex(item => item.classList.contains('dropdown-item-highlighted'));

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                selectedIndex = Math.min(selectedIndex + 1, items.length - 1);
                updateHighlight(items, selectedIndex);
                break;

            case 'ArrowUp':
                e.preventDefault();
                selectedIndex = Math.max(selectedIndex - 1, 0);
                updateHighlight(items, selectedIndex);
                break;

            case 'Enter':
                e.preventDefault();
                if (selectedIndex >= 0 && selectedIndex < items.length) {
                    items[selectedIndex].click();
                }
                break;

            case 'Escape':
                e.preventDefault();
                hideDropdown(dropdown.id);
                break;
        }
    });
}

// Update keyboard highlight
function updateHighlight(items, selectedIndex) {
    items.forEach(item => item.classList.remove('dropdown-item-highlighted'));

    if (selectedIndex >= 0 && selectedIndex < items.length) {
        items[selectedIndex].classList.add('dropdown-item-highlighted');
        items[selectedIndex].scrollIntoView({
            block: 'nearest',
            behavior: 'smooth'
        });
    }
}

// Hide dropdown
function hideDropdown(dropdownId) {
    const dropdown = document.getElementById(dropdownId);
    if (dropdown) {
        dropdown.style.display = 'none';
    }
}

// Hide dropdowns when clicking outside
function hideDropdownsOnClickOutside(e) {
    if (!e.target.closest('.dropdown-menu') && !e.target.closest('input')) {
        hideDropdown('sourceComputerDropdown');
        hideDropdown('targetComputerDropdown');
        hideDropdown('additionalGroupDropdown');
    }
}