import { Component, OnInit } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { InputTextModule } from 'primeng/inputtext';
import { InfoCardComponent, SpiderlyButtonComponent } from 'spiderly';
import { ConfigService } from 'src/app/business/services/config.service';
import { FormsModule } from "@angular/forms";
import { ApiService } from 'src/app/business/services/api/api.service';

@Component({
    templateUrl: './homepage.component.html',
    imports: [
    InfoCardComponent,
    TranslocoDirective,
    InputTextModule,
    FormsModule,
    SpiderlyButtonComponent
],
})
export class HomepageComponent implements OnInit {
  companyName = this.config.companyName;

  prompt: string;
  response: string;

  constructor(
    private config: ConfigService,
    private apiService: ApiService
  ) {}

  ngOnInit() {

  }

  saveProductsToDb() {
    this.apiService.saveProductsToVectorDb().subscribe();
  }

  sendMessage = () => {
    this.apiService.sendMessage(this.prompt).subscribe((response) => {
      this.response = response;
    });
  }

  ngOnDestroy(): void {
  }

}
