CREATE TABLE `${db_name}`.`index_field` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL,
	`index_id` INT NOT NULL,
	`field_id` INT NOT NULL,
	`ordering` ENUM (
		'ASC',
		'DESC'
	) NOT NULL,

	FOREIGN KEY (`index_id`)
		REFERENCES `index_base`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;
