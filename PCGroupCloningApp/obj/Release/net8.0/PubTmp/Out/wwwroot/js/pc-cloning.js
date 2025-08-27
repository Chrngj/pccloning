// wwwroot/js/pc-cloning.js - Complete version with keyboard navigation
class PCCloningApp {
    constructor() {
        this.sourceComputer = '';
        this.targetComputer = '';
        this.sourceComputerOU = '';
        this.selectedGroups = [];
        this.additionalGroups = [];
        this.searchTimeout = null;
        this.currentDropdown = null;
        this.selectedIndex = -1;

        this.initializeEventHandlers();
        this.setDefaultCheckboxState();
    }

    setDefaultCheckboxState() {
        // Sæt "Move to same OU" til checked som standard
        document.getElementById('moveToSameOU').checked = true;
    }

    initializeEventHandlers() {
        // Source computer search
        document.getElementById('sourceComputerSearch').addEventListener('input', (e) => {
            this.handleComputerSearch(e.target.value, 'source');
        });

        document.getElementById('sourceComputerSearch').addEventListener('keydown', (e) => {
            this.handleKeyDown(e, 'sourceComputerDropdown', 'source');
        });

        // Target computer search
        document.getElementById('targetComputerSearch').addEventListener('input', (e) => {
            this.handleComputerSearch(e.target.value, 'target');
        });

        document.getElementById('targetComputerSearch').addEventListener('keydown', (e) => {
            this.handleKeyDown(e, 'targetComputerDropdown', 'target');
        });

        // Additional group search
        document.getElementById('additionalGroupSearch').addEventListener('input', (e) => {
            this.handleGroupSearch(e.target.value);
        });

        document.getElementById('additionalGroupSearch').addEventListener('keydown', (e) => {
            this.handleKeyDown(e, 'additionalGroupDropdown', 'additionalGroup');
        });

        // Group selection buttons
        document.getElementById('selectAllGroups').addEventListener('click', () => {
            this.selectAllGroups(true);
        });

        document.getElementById('deselectAllGroups').addEventListener('click', () => {
            this.selectAllGroups(false);
        });

        // Execute clone button
        document.getElementById('executeClone').addEventListener('click', () => {
            this.executeClone();
        });

        // Hide dropdowns when clicking outside
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.dropdown-container')) {
                this.hideAllDropdowns();
            }
        });
    }

    handleKeyDown(e, dropdownId, type) {
        const dropdown = document.getElementById(dropdownId);

        if (dropdown.style.display === 'none') return;

        const items = dropdown.querySelectorAll('.dropdown-item');
        if (items.length === 0) return;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                this.selectedIndex = Math.min(this.selectedIndex + 1, items.length - 1);
                this.updateHighlight(items);
                this.currentDropdown = { dropdown, type, items };
                break;

            case 'ArrowUp':
                e.preventDefault();
                this.selectedIndex = Math.max(this.selectedIndex - 1, 0);
                this.updateHighlight(items);
                this.currentDropdown = { dropdown, type, items };
                break;

            case 'Enter':
                e.preventDefault();
                if (this.selectedIndex >= 0 && this.selectedIndex < items.length) {
                    const selectedItem = items[this.selectedIndex];
                    selectedItem.click();
                }
                break;

            case 'Escape':
                e.preventDefault();
                this.hideDropdown(dropdownId);
                this.resetSelection();
                break;
        }
    }

    updateHighlight(items) {
        // Remove all highlights
        items.forEach(item => item.classList.remove('dropdown-item-highlighted'));

        // Highlight selected item
        if (this.selectedIndex >= 0 && this.selectedIndex < items.length) {
            items[this.selectedIndex].classList.add('dropdown-item-highlighted');

            // Scroll into view if needed
            items[this.selectedIndex].scrollIntoView({
                block: 'nearest',
                behavior: 'smooth'
            });
        }
    }

    resetSelection() {
        this.selectedIndex = -1;
        this.currentDropdown = null;
    }

    resetFromStep(step) {
        // Reset kun specifikke ting, ikke skjul kort
        if (step <= 1) {
            // Reset source computer info
            this.sourceComputer = '';
            this.sourceComputerOU = '';
            this.selectedGroups = [];
            document.getElementById('sourceComputerInfo').style.display = 'none';
        }

        if (step <= 2) {
            // Reset target computer UDEN at skjule kort
            this.targetComputer = '';
            document.getElementById('targetComputerInfo').style.display = 'none';
        }

        if (step <= 3) {
            // Reset additional groups
            this.additionalGroups = [];
            document.getElementById('additionalGroupSearch').value = '';
            this.updateAdditionalGroupsList();
        }

        if (step <= 4) {
            // Reset preview and results
            this.updatePreview();
            document.getElementById('resultsCard').style.display = 'none';
        }

        // Update execute button state
        this.updateExecuteButton();
    }

    async handleComputerSearch(term, type) {
        clearTimeout(this.searchTimeout);

        if (term.length < 2) {
            this.hideDropdown(type + 'ComputerDropdown');
            return;
        }

        this.searchTimeout = setTimeout(async () => {
            try {
                const response = await fetch(`/api/computer/search?term=${encodeURIComponent(term)}`);
                const computers = await response.json();
                this.displayComputerDropdown(computers, type);
            } catch (error) {
                console.error('Error searching computers:', error);
                this.showError('Error searching for computers');
            }
        }, 300);
    }

    displayComputerDropdown(computers, type) {
        const dropdown = document.getElementById(type + 'ComputerDropdown');
        this.resetSelection(); // Reset keyboard selection

        if (computers.length === 0) {
            dropdown.innerHTML = '<div class="dropdown-item-text">No computers found</div>';
        } else {
            dropdown.innerHTML = computers.map((computer, index) =>
                `<button type="button" class="dropdown-item" data-index="${index}" onclick="app.selectComputer('${computer}', '${type}')">${computer}</button>`
            ).join('');
        }

        dropdown.style.display = 'block';
    }

    async selectComputer(computerName, type) {
        if (type === 'source') {
            // Reset kun source data
            this.resetFromStep(1);

            this.sourceComputer = computerName;
            document.getElementById('sourceComputerSearch').value = computerName;
            document.getElementById('selectedSourceComputer').textContent = computerName;

            // Get computer OU and groups
            await this.loadSourceComputerInfo(computerName);

        } else if (type === 'target') {
            this.targetComputer = computerName;
            document.getElementById('targetComputerSearch').value = computerName;
            document.getElementById('selectedTargetComputer').textContent = computerName;
            document.getElementById('targetComputerInfo').style.display = 'block';

            this.updatePreview();
            this.updateExecuteButton();
        }

        this.hideDropdown(type + 'ComputerDropdown');
    }

    async loadSourceComputerInfo(computerName) {
        try {
            // Load computer OU
            const ouResponse = await fetch(`/api/computer/${encodeURIComponent(computerName)}/ou`);
            const ouData = await ouResponse.json();
            this.sourceComputerOU = ouData.ou;
            document.getElementById('sourceComputerOU').textContent = ouData.ou || 'Unknown';

            // Load computer groups
            const groupsResponse = await fetch(`/api/computer/${encodeURIComponent(computerName)}/groups`);
            const groups = await groupsResponse.json();

            this.displaySourceGroups(groups);
            document.getElementById('sourceComputerInfo').style.display = 'block';

        } catch (error) {
            console.error('Error loading computer info:', error);
        }
    }

    displaySourceGroups(groups) {
        // VIGTIGT: Reset selected groups når vi loader nye grupper
        this.selectedGroups = [];

        const container = document.getElementById('sourceGroupsList');
        document.getElementById('groupCount').textContent = groups.length;

        if (groups.length === 0) {
            container.innerHTML = '<div class="col-12"><div class="alert alert-warning">No groups found for this computer</div></div>';
            return;
        }

        container.innerHTML = groups.map(group => `
            <div class="col-md-6 mb-2">
                <div class="form-check">
                    <input class="form-check-input group-checkbox" type="checkbox" value="${group}" id="group-${group}" checked>
                    <label class="form-check-label" for="group-${group}">
                        ${group}
                    </label>
                </div>
            </div>
        `).join('');

        // Update selected groups
        this.updateSelectedGroups();

        // Add event listeners to checkboxes
        container.querySelectorAll('.group-checkbox').forEach(checkbox => {
            checkbox.addEventListener('change', () => this.updateSelectedGroups());
        });
    }

    updateSelectedGroups() {
        const checkboxes = document.querySelectorAll('.group-checkbox:checked');
        this.selectedGroups = Array.from(checkboxes).map(cb => cb.value);

        // Opdater preview og execute button
        this.updatePreview();
        this.updateExecuteButton();
    }

    selectAllGroups(select) {
        const checkboxes = document.querySelectorAll('.group-checkbox');
        checkboxes.forEach(checkbox => {
            checkbox.checked = select;
        });
        this.updateSelectedGroups();
    }

    async handleGroupSearch(term) {
        clearTimeout(this.searchTimeout);

        if (term.length < 2) {
            this.hideDropdown('additionalGroupDropdown');
            return;
        }

        this.searchTimeout = setTimeout(async () => {
            try {
                const response = await fetch(`/api/groups/search?term=${encodeURIComponent(term)}`);
                const groups = await response.json();
                this.displayGroupDropdown(groups);
            } catch (error) {
                console.error('Error searching groups:', error);
                this.showError('Error searching for groups');
            }
        }, 300);
    }

    displayGroupDropdown(groups) {
        const dropdown = document.getElementById('additionalGroupDropdown');
        this.resetSelection(); // Reset keyboard selection

        if (groups.length === 0) {
            dropdown.innerHTML = '<div class="dropdown-item-text">No groups found</div>';
        } else {
            dropdown.innerHTML = groups.map((group, index) =>
                `<button type="button" class="dropdown-item" data-index="${index}" onclick="app.addAdditionalGroup('${group}')">${group}</button>`
            ).join('');
        }

        dropdown.style.display = 'block';
    }

    addAdditionalGroup(groupName) {
        if (!this.additionalGroups.includes(groupName) && !this.selectedGroups.includes(groupName)) {
            this.additionalGroups.push(groupName);
            this.updateAdditionalGroupsList();
            document.getElementById('additionalGroupSearch').value = '';
            this.updatePreview();
        }
        this.hideDropdown('additionalGroupDropdown');
    }

    updateAdditionalGroupsList() {
        const container = document.getElementById('additionalGroupsList');

        if (this.additionalGroups.length === 0) {
            container.innerHTML = '<small class="text-muted">No additional groups selected</small>';
            return;
        }

        container.innerHTML = this.additionalGroups.map(group => `
            <span class="badge bg-secondary me-2 mb-2">
                ${group}
                <button type="button" class="btn-close btn-close-white ms-1" onclick="app.removeAdditionalGroup('${group}')" aria-label="Remove"></button>
            </span>
        `).join('');
    }

    removeAdditionalGroup(groupName) {
        this.additionalGroups = this.additionalGroups.filter(g => g !== groupName);
        this.updateAdditionalGroupsList();
        this.updatePreview();
    }

    updatePreview() {
        document.getElementById('previewSource').textContent = this.sourceComputer || 'Not selected';
        document.getElementById('previewTarget').textContent = this.targetComputer || 'Not selected';
        document.getElementById('previewGroupCount').textContent = this.selectedGroups.length;
        document.getElementById('previewAdditionalCount').textContent = this.additionalGroups.length;
    }

    updateExecuteButton() {
        const executeButton = document.getElementById('executeClone');
        if (!executeButton) return; // Safety check

        const canExecute = this.sourceComputer && this.targetComputer;

        executeButton.disabled = !canExecute;

        if (!canExecute) {
            executeButton.innerHTML = '<i class="fas fa-play"></i> Select computers to continue';
        } else {
            executeButton.innerHTML = '<i class="fas fa-play"></i> Execute Clone Operation';
        }
    }

    async executeClone() {
        const executeButton = document.getElementById('executeClone');
        executeButton.disabled = true;
        executeButton.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Executing...';

        const request = {
            sourceComputer: this.sourceComputer,
            targetComputer: this.targetComputer,
            selectedGroups: this.selectedGroups,
            additionalGroups: this.additionalGroups,
            moveToSameOU: document.getElementById('moveToSameOU').checked,
            sourceComputerOU: this.sourceComputerOU
        };

        try {
            const response = await fetch('/api/clone/execute', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(request)
            });

            const result = await response.json();
            this.displayResults(result);

        } catch (error) {
            console.error('Error executing clone:', error);
            this.showError('Error executing clone operation');
        } finally {
            executeButton.disabled = false;
            executeButton.innerHTML = '<i class="fas fa-play"></i> Execute Clone Operation';
        }
    }

    displayResults(result) {
        const resultsCard = document.getElementById('resultsCard');
        const resultsContent = document.getElementById('resultsContent');

        if (result.success) {
            resultsContent.innerHTML = `
                <div class="alert alert-success">
                    <h6><i class="fas fa-check-circle"></i> ${result.message}</h6>
                    <ul class="mb-0">
                        <li>Successful operations: ${result.successCount}</li>
                        ${result.errorCount > 0 ? `<li>Failed operations: ${result.errorCount}</li>` : ''}
                    </ul>
                </div>
                ${result.operations && result.operations.length > 0 ? `
                    <div class="alert alert-info">
                        <h6>Operations performed:</h6>
                        <ul class="mb-0">
                            ${result.operations.map(op => `<li>${op}</li>`).join('')}
                        </ul>
                    </div>
                ` : ''}
                ${result.errors && result.errors.length > 0 ? `
                    <div class="alert alert-warning">
                        <h6>Errors encountered:</h6>
                        <ul class="mb-0">
                            ${result.errors.map(error => `<li>${error}</li>`).join('')}
                        </ul>
                    </div>
                ` : ''}
                <div class="mt-3">
                    <button type="button" class="btn btn-primary" onclick="location.reload()">
                        <i class="fas fa-redo"></i> Start New Clone Operation
                    </button>
                </div>
            `;
        } else {
            resultsContent.innerHTML = `
                <div class="alert alert-danger">
                    <h6><i class="fas fa-exclamation-triangle"></i> Operation Failed</h6>
                    <p>${result.message}</p>
                    ${result.error ? `<small>Error: ${result.error}</small>` : ''}
                    ${result.errors && result.errors.length > 0 ? `
                        <div class="mt-2">
                            <strong>Specific errors:</strong>
                            <ul class="mb-0">
                                ${result.errors.map(error => `<li>${error}</li>`).join('')}
                            </ul>
                        </div>
                    ` : ''}
                </div>
                <div class="mt-3">
                    <button type="button" class="btn btn-primary" onclick="location.reload()">
                        <i class="fas fa-redo"></i> Try Again
                    </button>
                </div>
            `;
        }

        resultsCard.style.display = 'block';
        resultsCard.scrollIntoView({ behavior: 'smooth' });
    }

    hideDropdown(dropdownId) {
        document.getElementById(dropdownId).style.display = 'none';
        this.resetSelection(); // Reset keyboard selection when hiding
    }

    hideAllDropdowns() {
        this.hideDropdown('sourceComputerDropdown');
        this.hideDropdown('targetComputerDropdown');
        this.hideDropdown('additionalGroupDropdown');
        this.resetSelection();
    }

    showError(message) {
        // Simple error display - could be enhanced with toast notifications
        alert(message);
    }
}

// Initialize the app when the page loads
let app;
document.addEventListener('DOMContentLoaded', function () {
    app = new PCCloningApp();
});