package ado

// TeamProject represents an Azure DevOps team project
type TeamProject struct {
	ID   string `json:"id"`
	Name string `json:"name"`
}

// Repository represents an Azure DevOps repository
type Repository struct {
	ID         string `json:"id"`
	Name       string `json:"name"`
	Size       uint64 `json:"size,string"` // ADO returns size as string
	IsDisabled bool   `json:"isDisabled,string"`
}

// teamProjectsResponse is the response from the projects list API
type teamProjectsResponse struct {
	Value []TeamProject `json:"value"`
}

// repositoriesResponse is the response from the repositories list API
type repositoriesResponse struct {
	Value []Repository `json:"value"`
}

// serviceEndpoint represents a service connection endpoint
type serviceEndpoint struct {
	ID   string `json:"id"`
	Type string `json:"type"`
	Name string `json:"name"`
}

// serviceEndpointsResponse is the response from the service endpoints API
type serviceEndpointsResponse struct {
	Value []serviceEndpoint `json:"value"`
}
