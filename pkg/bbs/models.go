package bbs

// Project represents a Bitbucket Server project
type Project struct {
	ID   int    `json:"id"`
	Key  string `json:"key"`
	Name string `json:"name"`
}

// Repository represents a Bitbucket Server repository
type Repository struct {
	ID   int    `json:"id"`
	Slug string `json:"slug"`
	Name string `json:"name"`
}

// projectsResponse is the paginated response from the projects API
type projectsResponse struct {
	Values        []Project `json:"values"`
	Size          int       `json:"size"`
	IsLastPage    bool      `json:"isLastPage"`
	Start         int       `json:"start"`
	Limit         int       `json:"limit"`
	NextPageStart int       `json:"nextPageStart,omitempty"`
}

// repositoriesResponse is the paginated response from the repositories API
type repositoriesResponse struct {
	Values        []Repository `json:"values"`
	Size          int          `json:"size"`
	IsLastPage    bool         `json:"isLastPage"`
	Start         int          `json:"start"`
	Limit         int          `json:"limit"`
	NextPageStart int          `json:"nextPageStart,omitempty"`
}
